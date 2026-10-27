using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Island;
internal static class UsageReader
{
    internal static string Read(string path)
    {
        try
        {
            var info = new FileInfo(path); if (!info.Exists) return "Log file moved or unavailable."; if (info.Length > 30_000_000) return "Log exceeds the 30 MB import limit. Choose a session log.";
            long tokens = 0, cumulative = 0; decimal cost = 0; bool costKnown = false; int records = 0; var ids = new HashSet<string>(); string limits = "Rate-limit windows are not reported in this file.";
            foreach (string line in File.ReadLines(path).Where(l => l.Length is > 0 and < 1_000_000))
            {
                try
                {
                    using var document = JsonDocument.Parse(line); var root = document.RootElement;
                    if (root.TryGetProperty("uuid", out var uuid) && !ids.Add(uuid.ToString())) continue;
                    if (root.TryGetProperty("message", out var message) && message.TryGetProperty("id", out var messageId) && !ids.Add("msg:" + messageId.ToString())) continue;
                    if (Find(root, "total_token_usage") is { ValueKind: JsonValueKind.Object } total && total.TryGetProperty("total_tokens", out var totalTokens)) { cumulative = Math.Max(cumulative, totalTokens.GetInt64()); records++; }
                    else if (Find(root, "usage") is { ValueKind: JsonValueKind.Object } usage)
                    {
                        foreach (string field in new[] { "input_tokens", "output_tokens", "cache_read_input_tokens", "cache_creation_input_tokens" }) if (usage.TryGetProperty(field, out var number) && number.TryGetInt64(out long value)) tokens += Math.Max(0, value); records++;
                    }
                    else if (root.TryGetProperty("tokens", out var value) && value.TryGetInt64(out long count)) { tokens += Math.Max(0, count); records++; }
                    var costValue = Find(root, "costUsd") ?? Find(root, "cost_usd"); if (costValue.HasValue && costValue.Value.TryGetDecimal(out decimal amount)) { cost += Math.Max(0, amount); costKnown = true; }
                    if (Find(root, "rate_limits") is { ValueKind: JsonValueKind.Object } rate) limits = "Reported rate limits: " + rate.GetRawText();
                }
                catch (JsonException) { }
            }
            return records == 0 ? "No recognized usage records. Normalized format: {\"tokens\":123,\"costUsd\":0.01}." : $"{Math.Max(tokens, cumulative):N0} reported tokens\n{(costKnown ? "$" + cost.ToString("0.0000", CultureInfo.InvariantCulture) + " reported cost" : "Cost unavailable · no price assumptions")}\n{limits}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException) { return "Could not read usage: " + ex.Message; }
    }
    private static JsonElement? Find(JsonElement node, string property, int depth = 0)
    {
        if (depth > 7 || node.ValueKind != JsonValueKind.Object) return null; if (node.TryGetProperty(property, out var result)) return result;
        foreach (var child in node.EnumerateObject()) { var nested = Find(child.Value, property, depth + 1); if (nested.HasValue) return nested; } return null;
    }
}

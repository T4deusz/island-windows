using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Island;
internal static class Lyrics
{
    internal static List<(double Time, string Text)> Parse(string lrc)
    {
        var result = new List<(double, string)>();
        foreach (string line in lrc.Split('\n').Take(5000))
        {
            var matches = Regex.Matches(line, @"\[(\d{1,3}):(\d{2}(?:\.\d{1,3})?)\]");
            if (matches.Count == 0) continue; string text = line[(matches[^1].Index + matches[^1].Length)..].Trim();
            foreach (Match match in matches) if (double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)) result.Add((int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + seconds, text));
        }
        return result.OrderBy(x => x.Item1).ToList();
    }
    internal static string At(List<(double Time, string Text)> lines, double position) => lines.LastOrDefault(l => l.Time <= position).Text ?? "";
}

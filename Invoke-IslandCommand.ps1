param(
    [Parameter(Mandatory=$true)][scriptblock]$Command,
    [string]$Title = 'Shell command',
    [string]$Token = $env:ISLAND_API_TOKEN
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Token)) { throw 'Set ISLAND_API_TOKEN to the token copied from Workspace > Developer. Enable the local API first.' }
$activityId = 'shell-' + [Guid]::NewGuid().ToString('N')
function Send-IslandActivity([string]$Kind, [double]$Progress) {
    $payload = @{ id=$activityId; title=$Title; kind=$Kind; progress=$Progress } | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri 'http://127.0.0.1:9999/activities' -Method Post -Headers @{ Authorization="Bearer $Token" } -ContentType 'application/json' -Body $payload -TimeoutSec 3 | Out-Null
}
Send-IslandActivity 'build' 0
try {
    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command exited with code $LASTEXITCODE" }
    Send-IslandActivity 'complete' 1
} catch {
    try { Send-IslandActivity 'error' 1 } catch { Write-Warning 'The failure could not be reported to Island.' }
    throw
}

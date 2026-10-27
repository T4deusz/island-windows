param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $arguments = @('publish', 'Island.csproj', '-c', 'Release', '-r', 'win-x64', '-o', '../Island-Windows-1.3', '--self-contained', $SelfContained.IsPresent.ToString().ToLowerInvariant())
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Built: ../Island-Windows-1.3/Island.exe'
} finally { Pop-Location }

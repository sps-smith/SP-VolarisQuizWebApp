param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "tests\Volaris101.Tests\Volaris101.Tests.csproj"
$resultsRoot = Join-Path $root "artifacts\coverage"
$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resultsDir = Join-Path $resultsRoot "test-results-$runStamp"
$coverageDir = Join-Path $resultsRoot "coverage"
$coverageFile = Join-Path $coverageDir "coverage.cobertura.xml"

New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
New-Item -ItemType Directory -Force -Path $coverageDir | Out-Null

dotnet test $project `
    --configuration $Configuration `
    --collect:"XPlat Code Coverage" `
    --results-directory $resultsDir

$latestCoverage = Get-ChildItem -Path $resultsDir -Recurse -Filter coverage.cobertura.xml |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latestCoverage) {
    throw "Coverage file not found in $resultsDir."
}

Copy-Item -Force $latestCoverage.FullName $coverageFile
Write-Host "Coverage written to $coverageFile"

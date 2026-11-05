$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "Test-Coverage.ps1"
& $script @args

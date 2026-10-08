<#
.SYNOPSIS
    One command before every deploy of a "superb" site app: unit tests, then the app started locally,
    then the browser (Playwright) flow tests against it. Stops at the first red test.

.EXAMPLE
    .\test-app.ps1 -App Notatskriver
    .\test-app.ps1 -App Notatskriver -Live      # flow tests against the live site instead of a local copy
#>
param(
    [Parameter(Mandatory)][ValidateSet("Notatskriver", "Tilbud")][string]$App,
    [switch]$Live
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Per app: unit test project, server project, local port, live address, flow test class, env var the flow tests read.
$apps = @{
    "Notatskriver" = @{
        Tests = "ITMartinNotatskriver.Tests"; Server = "ITMartinNotatskriver.Server"; Port = 5145
        LiveUrl = "https://notatskriver.itmartin.dk"; Flow = "NotatskriverFlowTests"; BaseVar = "NOTATSKRIVER_BASE"
        # The flow tests never reach the AI, so a dummy key is enough locally.
        Env = @{ "Claude__ApiKey" = "test-not-used" }
    }
    "Tilbud" = @{
        Tests = "ITMartinTilbud.Tests"; Server = "ITMartinTilbud.Server"; Port = 5150
        LiveUrl = "https://tilbud.itmartin.dk"; Flow = "TilbudFlowTests"; BaseVar = "TILBUD_BASE"
        # A PIN so the faste-tilbud guard is active; the flow test only tries a wrong one.
        Env = @{ "Tilbud__AdminPin" = "local-test-pin"; "Tilbud__DataDir" = (Join-Path $env:TEMP "tilbud-test-data") }
    }
}
$a = $apps[$App]

Write-Host "`n[1/3] Unit tests ($($a.Tests))" -ForegroundColor Cyan
dotnet test $a.Tests --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }

$server = $null
try {
    if ($Live) {
        $base = $a.LiveUrl
        Write-Host "`n[2/3] Using the live app: $base" -ForegroundColor Cyan
    } else {
        $base = "http://localhost:$($a.Port)"
        Write-Host "`n[2/3] Starting $($a.Server) on $base" -ForegroundColor Cyan
        foreach ($k in $a.Env.Keys) { Set-Item "env:$k" $a.Env[$k] }
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $server = Start-Process dotnet -ArgumentList "run --project $($a.Server) --no-launch-profile --urls $base" -PassThru -WindowStyle Hidden
        $up = $false
        for ($i = 0; $i -lt 60 -and -not $up; $i++) {
            Start-Sleep 2
            try { $up = (Invoke-WebRequest $base -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { }
        }
        if (-not $up) { throw "$($a.Server) did not start on $base" }
    }

    Write-Host "`n[3/3] Browser tests ($($a.Flow))" -ForegroundColor Cyan
    Set-Item "env:$($a.BaseVar)" $base
    dotnet test ITMartinTests --nologo -v q --filter "FullyQualifiedName~$($a.Flow)"
    if ($LASTEXITCODE -ne 0) { throw "Browser tests failed" }
    Write-Host "`nAll green - $App is ready to deploy." -ForegroundColor Green
}
finally {
    if ($server) {
        # dotnet run starts the app as a child process: stop the whole tree
        Get-CimInstance Win32_Process -Filter "ParentProcessId=$($server.Id)" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}

<#
.SYNOPSIS
    Report-only scan for rotated images in a library folder - lists which
    files need rotation and by how many degrees, but changes nothing. Uses
    the same free (no AI cost) face-detection check as the fix-orientation-
    free debug endpoint, just without the write step.

.PARAMETER Path
    The library folder to scan (e.g. "D:\mie\Billeder").

.EXAMPLE
    .\detect-rotated.ps1 -Path "D:\mie\Billeder"
#>

param(
    [Parameter(Mandatory = $true)][string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) {
    Write-Error "Path not found: $Path"
    exit 1
}
$fullPath = (Resolve-Path $Path).Path

$repoRoot = $PSScriptRoot
$scratchDb = Join-Path $env:TEMP "filesorter-check-scratch"
New-Item -ItemType Directory -Force -Path $scratchDb | Out-Null

$port = 5299
$baseUrl = "http://localhost:$port"

$env:MediaSettings__LibraryRoot = $scratchDb
Remove-Item Env:MediaSettings__SourceRoot -ErrorAction SilentlyContinue
Remove-Item Env:MediaSettings__ClientSlug -ErrorAction SilentlyContinue

Write-Host "Building FileSorter.Server..."
& dotnet build "$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj" --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Starting throwaway FileSorter.Server on $baseUrl ..."
$logFile = Join-Path $scratchDb "server.log"
$errFile = Join-Path $scratchDb "server.err.log"
$server = Start-Process -PassThru -WindowStyle Hidden dotnet `
    -ArgumentList "run --project `"$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj`" --urls $baseUrl --no-launch-profile --no-build" `
    -RedirectStandardOutput $logFile -RedirectStandardError $errFile

try {
    $ready = $false
    $probeUri = "$baseUrl/api/debug/p4-verify-structure?path=$([uri]::EscapeDataString($scratchDb))"
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        try {
            Invoke-WebRequest -Uri $probeUri -Method Post -UseBasicParsing -TimeoutSec 10 | Out-Null
            $ready = $true
            break
        } catch {
            if ($_.Exception.Response) { $ready = $true; break }
            if ($server.HasExited) {
                Get-Content $logFile -ErrorAction SilentlyContinue | Write-Host
                Get-Content $errFile -ErrorAction SilentlyContinue | Write-Host
                Write-Error "FileSorter.Server exited before becoming ready."
                exit 1
            }
        }
    }
    if (-not $ready) { Write-Error "FileSorter.Server did not become ready in time."; exit 1 }

    Write-Host "Scanning for rotated images in: $fullPath (this can take a while - one face-detection pass per image)"
    $encodedPath = [uri]::EscapeDataString($fullPath)
    $result = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/debug/detect-rotated-images?path=$encodedPath" -TimeoutSec 3600

    $downloadsDir = Join-Path $env:USERPROFILE "Downloads"
    $reportName = "rotated-images_$((Get-Item $fullPath).Name)_$(Get-Date -Format 'yyyy-MM-dd_HHmm').json"
    $reportPath = Join-Path $downloadsDir $reportName
    $result | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "=== Rotated images in $fullPath ==="
    Write-Host "Checked: $($result.photosChecked)"
    Write-Host "Needs rotation: $($result.rotatedImages.Count)"
    Write-Host "Needs manual review (inconclusive): $($result.needsManualReview.Count)"
    Write-Host ""
    foreach ($img in $result.rotatedImages) {
        Write-Host "  $($img.degreesNeeded)`u{00B0} - $($img.relativePath)"
    }
    Write-Host ""
    Write-Host "Full report: $reportPath"
}
finally {
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}

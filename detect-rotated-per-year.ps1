<#
.SYNOPSIS
    Scans a library folder for rotated images ONE YEAR AT A TIME, writing
    results to a running report file as soon as each year finishes - so you
    can start manually rotating already-scanned years while later ones are
    still being scanned. Report-only, changes nothing on disk.

.PARAMETER Path
    The library folder whose year subfolders (YYYY) should be scanned, e.g.
    "D:\mie\Billeder".

.EXAMPLE
    .\detect-rotated-per-year.ps1 -Path "D:\mie\Billeder"
#>

param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$StartAtYear,
    [string]$AppendToReport
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) { Write-Error "Path not found: $Path"; exit 1 }
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

$downloadsDir = Join-Path $env:USERPROFILE "Downloads"
if ($AppendToReport) {
    $reportPath = $AppendToReport
} else {
    $reportPath = Join-Path $downloadsDir "rotated-images_$((Get-Item $fullPath).Name)_$(Get-Date -Format 'yyyy-MM-dd_HHmm').txt"
    "Rotated images report - $fullPath - started $(Get-Date -Format 'yyyy-MM-dd HH:mm')" | Set-Content -Path $reportPath -Encoding UTF8
    "" | Add-Content -Path $reportPath -Encoding UTF8
}

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

    $years = Get-ChildItem $fullPath -Directory | Where-Object { $_.Name -match '^\d{4}$' -and (-not $StartAtYear -or $_.Name -ge $StartAtYear) } | Sort-Object Name
    $totalRotated = 0

    foreach ($yearDir in $years) {
        Write-Host ""
        Write-Host "=== Scanning $($yearDir.Name) ==="
        $encodedPath = [uri]::EscapeDataString($yearDir.FullName)
        $result = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/debug/detect-rotated-images?path=$encodedPath" -TimeoutSec 1800

        "=== $($yearDir.Name) : $($result.photosChecked) checked, $($result.rotatedImages.Count) need rotation, $($result.needsManualReview.Count) need manual review ===" | Add-Content -Path $reportPath -Encoding UTF8

        foreach ($img in $result.rotatedImages) {
            # Mark the file in place (stays in its correct date-range group,
            # just gets an "X_" prefix) so it's obvious in Explorer without
            # cross-referencing this report - easy to find, easy to search
            # for later, easy to strip back off once rotated.
            $srcPath = Join-Path $yearDir.FullName $img.relativePath
            $leaf = Split-Path $srcPath -Leaf
            if ($leaf -notlike "X_*") {
                $markedPath = Join-Path (Split-Path $srcPath -Parent) "X_$leaf"
                if ((Test-Path $srcPath) -and (-not (Test-Path $markedPath))) {
                    Rename-Item -Path $srcPath -NewName "X_$leaf"
                }
            }
            "  $($img.degreesNeeded)`u{00B0} - $($yearDir.Name)\$($img.relativePath)" | Add-Content -Path $reportPath -Encoding UTF8
        }
        "" | Add-Content -Path $reportPath -Encoding UTF8

        Write-Host "  Checked: $($result.photosChecked) | Need rotation: $($result.rotatedImages.Count) | Manual review: $($result.needsManualReview.Count)"
        $totalRotated += $result.rotatedImages.Count
    }

    Write-Host ""
    Write-Host "=== Done - $totalRotated images flagged for rotation across $($years.Count) years ==="
    Write-Host "Report: $reportPath"
}
finally {
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}

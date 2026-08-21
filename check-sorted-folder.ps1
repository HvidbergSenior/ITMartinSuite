<#
.SYNOPSIS
    Runs the pre-delivery-check (structure, extensions, free rotation-fix,
    duplicate scan) directly against a folder that should already be sorted -
    whether by Package1 or by hand afterward (camera-folder merges, manual
    cleanup, etc). This is the "did today's manual edits break anything"
    check, runnable on demand against any already-delivered library.

    Starts a throwaway local FileSorter.Server instance just to expose the
    debug endpoints (no RabbitMQ/Worker needed - these checks are read-mostly
    and don't touch the job queue), points it at a scratch DB so it never
    touches the target folder's own state, calls
    POST /api/debug/pre-delivery-check, prints a summary, and stops the
    server again. Never deletes anything - orientation/duplicate checks only
    report, per ILibraryPolishService's own contract.

.PARAMETER Path
    The already-sorted library folder to check (e.g. "D:\mie\Billeder" or
    "E:\Rico").

.EXAMPLE
    .\check-sorted-folder.ps1 -Path "D:\mie"
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

# Don't set ClientSlug - it would overwrite LibraryRoot below.
$env:MediaSettings__LibraryRoot = $scratchDb
Remove-Item Env:MediaSettings__SourceRoot -ErrorAction SilentlyContinue
Remove-Item Env:MediaSettings__ClientSlug -ErrorAction SilentlyContinue

Write-Host "Building FileSorter.Server (so startup below doesn't also compile)..."
& dotnet build "$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj" --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed - see output above."
    exit 1
}

Write-Host "Starting throwaway FileSorter.Server on $baseUrl ..."
$logFile = Join-Path $scratchDb "server.log"
$errFile = Join-Path $scratchDb "server.err.log"
$server = Start-Process -PassThru -WindowStyle Hidden dotnet `
    -ArgumentList "run --project `"$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj`" --urls $baseUrl --no-launch-profile --no-build" `
    -RedirectStandardOutput $logFile -RedirectStandardError $errFile

try {
    $ready = $false
    # Don't probe "/" - the Blazor home page synchronously connects to a
    # Docker-internal RabbitMQ host that doesn't exist on a local run, and
    # throws a 500 every time (RabbitMQ.Client.BrokerUnreachableException),
    # unrelated to whether the debug API endpoints we actually need are
    # ready. Probe one of those endpoints directly instead - ANY real HTTP
    # response (even a 4xx/5xx business error) means the server is up and
    # its DI graph resolved; only a connection failure means "not up yet".
    $probeUri = "$baseUrl/api/debug/p4-verify-structure?path=$([uri]::EscapeDataString($scratchDb))"
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        try {
            Invoke-WebRequest -Uri $probeUri -Method Post -UseBasicParsing -TimeoutSec 10 | Out-Null
            $ready = $true
            break
        } catch {
            if ($_.Exception.Response) {
                $ready = $true
                break
            }
            if ($server.HasExited) {
                Write-Host "--- server log ---"
                Get-Content $logFile -ErrorAction SilentlyContinue | Write-Host
                Get-Content $errFile -ErrorAction SilentlyContinue | Write-Host
                Write-Error "FileSorter.Server exited before becoming ready."
                exit 1
            }
        }
    }
    if (-not $ready) {
        Write-Error "FileSorter.Server did not become ready in time."
        exit 1
    }

    Write-Host "Running pre-delivery-check against: $fullPath"
    $encodedPath = [System.Uri]::EscapeDataString($fullPath)
    $result = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/debug/pre-delivery-check?path=$encodedPath" -TimeoutSec 1800

    $downloadsDir = Join-Path $env:USERPROFILE "Downloads"
    $reportName = "pre-delivery-check_$((Get-Item $fullPath).Name)_$(Get-Date -Format 'yyyy-MM-dd_HHmm').json"
    $reportPath = Join-Path $downloadsDir $reportName
    $result | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "=== Pre-delivery check: $fullPath ==="
    Write-Host "Integrity  (decode failures): $($result.integrity.failureCount)"
    Write-Host "Structure  (issues):          $($result.structure.issues.Count)"
    Write-Host "Delivery   (issues):          $($result.delivery.issues.Count)"
    Write-Host "Orientation (needs review):   $($result.orientation.needsManualReview.Count)"
    Write-Host "Duplicates (exact groups):    $(($result.duplicates.groups | Where-Object { $_.kind -eq 'exact' } | Measure-Object).Count)"
    Write-Host "Duplicates (near groups):     $(($result.duplicates.groups | Where-Object { $_.kind -eq 'near' } | Measure-Object).Count)"
    Write-Host ""
    Write-Host "Full report: $reportPath"

    $hasIssues = ($result.integrity.failureCount -gt 0) -or
                 ($result.structure.issues.Count -gt 0) -or
                 ($result.delivery.issues.Count -gt 0) -or
                 ($result.orientation.needsManualReview.Count -gt 0) -or
                 ($result.duplicates.groups.Count -gt 0)

    if ($hasIssues) {
        Write-Host "RESULT: ISSUES FOUND - see report above/full JSON for details." -ForegroundColor Yellow
        exit 1
    } else {
        Write-Host "RESULT: CLEAN - no structure, integrity, orientation, or duplicate issues found." -ForegroundColor Green
        exit 0
    }
}
finally {
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}

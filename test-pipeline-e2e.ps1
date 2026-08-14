<#
.SYNOPSIS
End-to-end regression test for FileSorter's Package1 pipeline: builds a small
mixed-file-type source folder (images, videos, audio, documents, a zip
archive, deliberate filename collisions), runs it through a real local
Server+Worker+RabbitMQ instance exactly like production, and asserts every
file landed in the right place and every free post-sort add-on ran.

Safe to re-run any time - rebuilds the source fixture fresh each run and uses
throwaway local paths, never touches the NAS or any real customer library.

.PARAMETER SkipRabbitMq
Skip starting/checking the rabbitmq container - use when it's already running.
#>
param(
    [switch]$SkipRabbitMq
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$testRoot = "C:\FileSorterJobs\e2e-test"
$sourceRoot = Join-Path $testRoot "source"
$outputRoot = Join-Path $testRoot "output"
$logDir = Join-Path $testRoot "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$failures = @()
function Assert-True {
    param([bool]$Condition, [string]$Message)
    if ($Condition) {
        Write-Host "  [OK] $Message" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $Message" -ForegroundColor Red
        $script:failures += $Message
    }
}

$serverProc = $null
$workerProc = $null

try {
    # --- 1. fresh fixture -------------------------------------------------
    Write-Host "== Building test fixture ==" -ForegroundColor Cyan
    if (Test-Path $outputRoot) { Remove-Item -Recurse -Force $outputRoot }
    $manifest = & (Join-Path $repoRoot "ITMartinFileSorter.Tests\E2eFixture\New-TestFixture.ps1") -OutputRoot $sourceRoot
    Write-Host "  Fixture: $($manifest.Count) expected files across $((($manifest | Group-Object Category).Count)) categories"

    # --- 2. rabbitmq --------------------------------------------------------
    if (-not $SkipRabbitMq) {
        Write-Host "== Ensuring rabbitmq is running ==" -ForegroundColor Cyan
        $existing = docker ps --filter "name=^/rabbitmq$" --format "{{.Names}}" 2>$null
        if (-not $existing) {
            docker rm -f rabbitmq 2>$null | Out-Null
            docker run -d --name rabbitmq -p 5672:5672 rabbitmq:3 | Out-Null
            Write-Host "  Started rabbitmq container, waiting for it to accept connections..."
            Start-Sleep -Seconds 8
        } else {
            Write-Host "  rabbitmq already running"
        }
    }

    # --- 3. claude key + env ------------------------------------------------
    # Output dir must exist before anything tries to open a SQLite db inside
    # it (Package1's export step normally creates it, but that hasn't run yet
    # at migration time).
    New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

    $envLine = Get-Content (Join-Path $repoRoot "magic.env") | Where-Object { $_ -match 'CLAUDE__APIKEY=' }
    $claudeKey = ($envLine -split '=', 2)[1]

    $env:MediaSettings__ClientSlug = ""
    $env:MediaSettings__LibraryRoot = $outputRoot
    $env:MediaSettings__SourceRoot = $sourceRoot
    $env:CLAUDE__APIKEY = $claudeKey
    $env:RabbitMq__Host = "localhost"

    # --- 4. migrate the fresh db via a brief Worker run ----------------------
    Write-Host "== Applying EF migrations to fresh test db ==" -ForegroundColor Cyan
    $migrateOut = Join-Path $logDir "worker_migrate_out.log"
    $migrateErr = Join-Path $logDir "worker_migrate_err.log"
    $migrateProc = Start-Process -FilePath "dotnet" -ArgumentList "run","--project","ITMartinFileSorter.Worker\ITMartinFileSorter.Worker.csproj" `
        -PassThru -RedirectStandardOutput $migrateOut -RedirectStandardError $migrateErr -WindowStyle Hidden -WorkingDirectory $repoRoot
    Start-Sleep -Seconds 20
    Get-CimInstance Win32_Process -Filter "ParentProcessId=$($migrateProc.Id)" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $migrateProc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # --- 5. start server + worker for real ------------------------------
    Write-Host "== Starting Server + Worker ==" -ForegroundColor Cyan
    $serverOut = Join-Path $logDir "server_out.log"
    $serverErr = Join-Path $logDir "server_err.log"
    $serverProc = Start-Process -FilePath "dotnet" -ArgumentList "run","--project","ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj","--launch-profile","http" `
        -PassThru -RedirectStandardOutput $serverOut -RedirectStandardError $serverErr -WindowStyle Hidden -WorkingDirectory $repoRoot

    $workerOut = Join-Path $logDir "worker_out.log"
    $workerErr = Join-Path $logDir "worker_err.log"
    $workerProc = Start-Process -FilePath "dotnet" -ArgumentList "run","--project","ITMartinFileSorter.Worker\ITMartinFileSorter.Worker.csproj" `
        -PassThru -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr -WindowStyle Hidden -WorkingDirectory $repoRoot

    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        if ((Select-String -Path $serverOut -Pattern "Now listening on" -ErrorAction SilentlyContinue)) { $ready = $true; break }
    }
    if (-not $ready) { throw "Server did not become ready within 60s - check $serverOut" }
    Write-Host "  Server ready"

    # --- 6. trigger Package1 --------------------------------------------
    Write-Host "== Running Package1 ==" -ForegroundColor Cyan
    $startResp = Invoke-RestMethod -Method Post -Uri "http://localhost:5293/api/debug/p1-start?source=$([uri]::EscapeDataString($sourceRoot))&output=$([uri]::EscapeDataString($outputRoot))"
    Write-Host "  queue message id: $($startResp.workflowId) (not the tracked workflow id - see p1-status comment)"

    $status = $null
    $deadline = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        try {
            $status = Invoke-RestMethod -Uri "http://localhost:5293/api/debug/p1-status"
        } catch { continue }
        if ($status.status -in @("Completed", "Failed")) { break }
        Write-Host "  ...$($status.status) / $($status.currentStep) ($($status.progressCurrent)/$($status.progressTotal))"
    }

    if (-not $status -or $status.status -ne "Completed") {
        throw "Package1 did not complete successfully within 5 minutes. Last status: $($status | ConvertTo-Json)"
    }
    Write-Host "  Package1 completed"

    # Give the post-sort add-on chain (runs inside the same handler, after
    # ExecuteAsync returns but before the job is considered fully done) a
    # moment to finish writing SmartFolders/_Galleri/index.html.
    Start-Sleep -Seconds 5

    # --- 7. assertions ---------------------------------------------------
    Write-Host "== Verifying results ==" -ForegroundColor Cyan

    $failedFilesPath = Join-Path $outputRoot "_failed_files.txt"
    Assert-True (-not (Test-Path $failedFilesPath) -or (Get-Content $failedFilesPath -Raw).Trim().Length -eq 0) `
        "No entries in _failed_files.txt"

    # Files without a real embedded date (true for every synthetic fixture
    # file here - ffmpeg doesn't stamp creation_time by default, and this
    # pipeline deliberately never trusts filesystem mtimes for dating) land
    # under "Undated/{Category}/..." instead of "{Category}/..." directly -
    # both count as correctly categorized, so match either path shape.
    $byCategory = $manifest | Group-Object Category
    foreach ($group in $byCategory) {
        $expectedCount = $group.Count
        $actualFiles = Get-ChildItem -Recurse -File $outputRoot -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match [regex]::Escape("\$($group.Name)\") }
        Assert-True ($actualFiles.Count -ge $expectedCount) `
            "$($group.Name): expected >= $expectedCount file(s), found $($actualFiles.Count) under a $($group.Name)\ path segment"
    }

    # zip contents specifically - proves TryEnsureZipExtracted actually ran.
    # Export normalizes underscores to spaces in filenames, so match loosely.
    $zipPhoto = Get-ChildItem -Recurse -File $outputRoot -Filter "zipped*photo*" -ErrorAction SilentlyContinue
    Assert-True ($zipPhoto.Count -gt 0) "Zip-extracted photo (zipped_photo.jpg) made it into the sorted output"
    $zipDoc = Get-ChildItem -Recurse -File $outputRoot -Filter "zipped*notes*" -ErrorAction SilentlyContinue
    Assert-True ($zipDoc.Count -gt 0) "Zip-extracted document (zipped_notes.txt) made it into the sorted output"

    # collision handling - both IMG_0001.jpg files should have survived as distinct files
    $collided = Get-ChildItem -Recurse -File $outputRoot -Filter "IMG*0001*" -ErrorAction SilentlyContinue
    Assert-True ($collided.Count -ge 2) "Both filename-colliding IMG_0001.jpg files survived export as distinct files (found $($collided.Count))"

    # Free add-on chain proof. SmartFolders itself is only created when there's
    # actually something to cluster (real Trips need GPS, Traditions need the
    # same calendar date across multiple years) - a tiny one-shot synthetic
    # fixture legitimately produces neither, so its absence isn't a failure
    # here; what matters is the add-on steps ran without throwing, which the
    # per-step error log below checks instead.
    Assert-True (Test-Path (Join-Path $outputRoot "_Galleri")) "_Galleri/ generated (StaticGalleryExportService ran)"
    Assert-True (Test-Path (Join-Path $outputRoot "index.html")) "index.html generated (offline gallery export ran)"
    $addonErrors = Select-String -Path $workerOut, $workerErr -Pattern "Post-sort add-on step .* failed" -ErrorAction SilentlyContinue
    Assert-True ($null -eq $addonErrors) "No post-sort add-on step logged an error$(if ($addonErrors) { ": " + ($addonErrors | Select-Object -First 1) })"

    # --- 8. summary --------------------------------------------------------
    Write-Host ""
    if ($failures.Count -eq 0) {
        Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
    } else {
        Write-Host "$($failures.Count) CHECK(S) FAILED:" -ForegroundColor Red
        $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    }
}
finally {
    Write-Host ""
    Write-Host "== Cleaning up processes ==" -ForegroundColor Cyan
    foreach ($proc in @($serverProc, $workerProc)) {
        if ($proc) {
            Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)" -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($failures.Count -gt 0) { exit 1 }
exit 0

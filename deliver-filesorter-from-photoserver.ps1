<#
.SYNOPSIS
Pulls a FileSorter QuickSort run's output from the photoserver back to this
desktop in two waves, over SSH (the photoserver has no way to reach IN to
this desktop - confirmed 2026-09-03, no SSH server/SMB share here - so this
runs as a pull, initiated from here, not a push from the photoserver).

Wave 1 fires as soon as QuickSort's own fast pass completes (p1-status):
everything except the Videoer folder - photos/audio/docs/etc. Wave 2 fires
once every dispatched video conversion has actually finished
(quickSort-video-status, a separate signal from p1-status since video work
now runs concurrently in the background and can finish hours after QuickSort
itself reports Completed - see VideoConvertFinalizeWorkflowStep).

NOTE: written 2026-09-03, before FileSorter is actually deployed to the
photoserver. The status-endpoint port/path assumptions below match this
desktop's own local dev convention (Server on localhost:8080) but haven't
been verified against a real photoserver deployment yet - check these once
that exists.

.PARAMETER RemoteHost
SSH target for the photoserver, user@host form.

.PARAMETER RemoteLibraryPath
Path to the sorted library root on the photoserver (e.g. the client-slug
folder under /library, matching MediaSettings__LibraryRoot there).

.PARAMETER LocalDestination
Where to pull files to on this desktop.

.PARAMETER RemoteWebPort
Port FileSorter.Server listens on on the photoserver.
#>
param(
    [string]$RemoteHost = "martinhvidberg@10.0.0.200",
    [Parameter(Mandatory)]
    [string]$RemoteLibraryPath,
    [Parameter(Mandatory)]
    [string]$LocalDestination,
    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519",
    [int]$RemoteWebPort = 8080,
    [int]$PollIntervalSeconds = 120
)

$ErrorActionPreference = "Stop"

function Get-RemoteStatus {
    param([string]$Endpoint)

    $json = ssh -i $SshKey $RemoteHost "curl -s http://localhost:$RemoteWebPort$Endpoint"
    if ([string]::IsNullOrWhiteSpace($json)) { return $null }

    try { return $json | ConvertFrom-Json }
    catch { return $null }
}

function Copy-RemoteFolder {
    param([string]$FolderName)

    Write-Host "  Pulling $FolderName..." -ForegroundColor DarkGray
    $remoteSpec = "${RemoteHost}:${RemoteLibraryPath}/$FolderName"
    scp -i $SshKey -r $remoteSpec $LocalDestination 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "  scp failed for $FolderName (exit $LASTEXITCODE) - will retry next run"
    }
}

New-Item -ItemType Directory -Force -Path $LocalDestination | Out-Null

Write-Host "Waiting for QuickSort to complete on $RemoteHost ($RemoteLibraryPath)..." -ForegroundColor Cyan
while ($true) {
    $status = Get-RemoteStatus "/api/debug/p1-status"

    if ($status -and $status.status -eq "Completed") { break }
    if ($status -and $status.status -eq "Failed") {
        Write-Error "QuickSort failed on the photoserver: $($status.failureReason)"
        exit 1
    }

    if ($status) {
        Write-Host "  Still running - $($status.currentStep) $($status.progressCurrent)/$($status.progressTotal)" -ForegroundColor DarkGray
    }

    Start-Sleep -Seconds $PollIntervalSeconds
}

Write-Host "QuickSort completed. Pulling wave 1 (everything except Videoer)..." -ForegroundColor Green

$remoteFolders = (ssh -i $SshKey $RemoteHost "ls -1 '$RemoteLibraryPath'") -split "`n" |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and $_ -ne "Videoer" }

foreach ($folder in $remoteFolders) {
    Copy-RemoteFolder -FolderName $folder
}

Write-Host "Wave 1 delivered to $LocalDestination" -ForegroundColor Green

Write-Host "Waiting for all videos to finish converting..." -ForegroundColor Cyan
while ($true) {
    $videoStatus = Get-RemoteStatus "/api/debug/quicksort-video-status"

    if ($videoStatus -and $videoStatus.status -eq "Completed") { break }
    if (-not $videoStatus) {
        # 404 means the video-convert step hasn't run yet (or this run had
        # zero videos) - keep waiting, p1-status already confirmed the run
        # itself succeeded.
    }

    Start-Sleep -Seconds $PollIntervalSeconds
}

Write-Host "All videos converted. Pulling wave 2 (Videoer)..." -ForegroundColor Green
Copy-RemoteFolder -FolderName "Videoer"

Write-Host "Wave 2 delivered. Done." -ForegroundColor Green

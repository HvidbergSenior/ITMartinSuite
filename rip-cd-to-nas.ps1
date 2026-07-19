<#
    Rips every track off an audio CD and pushes the files straight to the NAS,
    in one command - no title typing, no separate upload step.

    Runs on the Windows HOST, not inside Docker - Docker Desktop's WSL2 backend
    cannot see a physical optical drive at all (same class of problem documented
    for FileSorter/MusikStudio's USB/network-mount issues - see feedback memory:
    WSL2 drive-mount timing), so this can never run from inside a container.

    Needs fre:ac (https://www.freac.org) installed, free and open source. Its
    command-line tool on Windows is freaccmd.exe (NOT freac_cli.exe - that name,
    used by this script's earlier sibling rip-cd.ps1, was a guess that turned out
    wrong; confirmed against the fre:ac GitHub repo).

    NOT YET VERIFIED ON REAL HARDWARE: no optical drive/fre:ac install was
    available while writing this, so freaccmd.exe's exact rip flags are a
    best-effort guess, not a proven recipe. Run it once against a real disc and
    check `freaccmd.exe --help` if the rip step doesn't behave as expected.

    Tracks are ripped as Track01.mp3, Track02.mp3, ... - no CDDB/manual lookup,
    deliberately, to keep this fast. Rename afterward if you want proper titles.
#>

param(
    [string]$DriveLetter = "D",
    [string]$Artist,
    [string]$Album
)

$NasHost = "martinhvidberg@100.117.120.44"
$NasBase = "/volume1/MartinMusik/RippedCDs"

$FreacCli = "${env:ProgramFiles}\fre:ac\freaccmd.exe"

if (-not (Test-Path $FreacCli)) {
    Write-Error @"
fre:ac's command-line tool wasn't found at:
  $FreacCli

Install fre:ac first: https://www.freac.org (free, open source).
This script has not been tested against freaccmd.exe's actual CLI arguments yet -
treat the rip command below as a starting point, not a proven recipe.
"@
    exit 1
}

if (-not (Test-Path "${DriveLetter}:\")) {
    Write-Error "No disc detected in drive ${DriveLetter}: - insert the CD and try again."
    exit 1
}

$folderName = if ($Artist -or $Album) {
    (@($Artist, $Album) -join " - ") -replace '[\\/:*?"<>|]', ''
} else {
    "CD-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
}

$tempOut = Join-Path $env:TEMP "cd-rip-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $tempOut -Force | Out-Null

Write-Host "[1/3] Ripping ${DriveLetter}: via fre:ac..." -ForegroundColor Cyan

# NOTE: exact freaccmd.exe flags unverified - check `freaccmd.exe --help`
# against your installed version and adjust before relying on this.
& $FreacCli --input "${DriveLetter}:" --output $tempOut --format mp3
if ($LASTEXITCODE -ne 0) { Write-Error "fre:ac rip failed."; exit 1 }

$rippedFiles = Get-ChildItem $tempOut -Filter *.mp3 | Sort-Object Name
if ($rippedFiles.Count -eq 0) {
    Write-Error "No tracks were ripped - nothing to push. Check freaccmd.exe's output above."
    Remove-Item $tempOut -Recurse -Force
    exit 1
}

for ($i = 0; $i -lt $rippedFiles.Count; $i++) {
    $trackNum = "{0:D2}" -f ($i + 1)
    $dest = Join-Path $tempOut "Track$trackNum.mp3"
    if ($rippedFiles[$i].FullName -ne $dest) {
        Move-Item $rippedFiles[$i].FullName $dest -Force
    }
}

Write-Host "[2/3] Pushing $($rippedFiles.Count) track(s) to NAS: $NasBase/$folderName ..." -ForegroundColor Cyan

$nasDir = "$NasBase/$folderName"
ssh $NasHost "mkdir -p '$nasDir'"
if ($LASTEXITCODE -ne 0) { Write-Error "Could not create $nasDir on the NAS."; exit 1 }

Get-ChildItem $tempOut -Filter *.mp3 | ForEach-Object {
    scp -O $_.FullName "${NasHost}:${nasDir}/$($_.Name)"
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to push $($_.Name)."; exit 1 }
    Write-Host "  -> $nasDir/$($_.Name)" -ForegroundColor Green
}

Write-Host "[3/3] Cleaning up local temp files..." -ForegroundColor Cyan
Remove-Item $tempOut -Recurse -Force

Write-Host "Done - $($rippedFiles.Count) track(s) on the NAS at $nasDir" -ForegroundColor Green

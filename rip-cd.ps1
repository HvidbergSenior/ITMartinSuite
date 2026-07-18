<#
    Rips an audio CD's tracks into the local karaoke library folder, named
    "Artist - Title.ext" so KaraokeLibraryService (ITMartinKaraoke.Server)
    picks them up automatically on its next folder scan.

    Runs on the Windows HOST, not inside Docker - Docker Desktop's WSL2
    backend cannot see a physical optical drive at all (same class of
    problem as the WSL2/USB-drive issue documented for FileSorter and
    MusikStudio - see feedback memory: WSL2 drive-mount timing), so ripping
    can never happen from inside the karaoke-web container itself.

    Needs fre:ac (https://www.freac.org) installed, which is free and has a
    working command-line mode ("freac_cli.exe") that this script calls.
    ffmpeg alone is NOT enough here: the prebuilt Windows ffmpeg binaries
    people normally install (gyan.dev / BtbN) are not compiled with libcdio
    support, so they cannot read audio CD tracks directly - only fre:ac (or
    a commercial ripper like Exact Audio Copy, not scripted here) reliably
    does digital audio extraction on Windows out of the box.

    NOT YET VERIFIED ON REAL HARDWARE: this machine currently has no optical
    drive attached and no ripping tool installed, so this script has only
    been checked for correct PowerShell syntax, not run against an actual
    CD. Run it once with a real disc in the drive and fix whatever fre:ac's
    actual CLI output looks like before relying on it.
#>

param(
    [string]$DriveLetter = "D",
    [string]$LibraryRoot = "C:\KaraokeLibraryLocal",
    [Parameter(Mandatory = $true)][string]$Artist,
    [Parameter(Mandatory = $true)][string[]]$TrackTitles  # one per track, in disc order
)

$FreacCli = "${env:ProgramFiles}\fre:ac\freac_cli.exe"

if (-not (Test-Path $FreacCli)) {
    Write-Error @"
fre:ac's command-line tool wasn't found at:
  $FreacCli

Install fre:ac first: https://www.freac.org (free, open source).
This script has not been tested against fre:ac's actual CLI arguments yet -
treat the rip command below as a starting point, not a proven recipe.
"@
    exit 1
}

if (-not (Test-Path "${DriveLetter}:\")) {
    Write-Error "No disc detected in drive ${DriveLetter}: - insert the CD and try again."
    exit 1
}

New-Item -ItemType Directory -Path $LibraryRoot -Force | Out-Null
$tempOut = Join-Path $env:TEMP "cd-rip-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $tempOut -Force | Out-Null

Write-Host "Ripping tracks from ${DriveLetter}: via fre:ac..." -ForegroundColor Cyan

# NOTE: exact fre:ac CLI flags unverified - check `freac_cli.exe --help`
# against your installed version and adjust before relying on this.
& $FreacCli --input "${DriveLetter}:" --output $tempOut --format mp3
if ($LASTEXITCODE -ne 0) { Write-Error "fre:ac rip failed."; exit 1 }

$rippedFiles = Get-ChildItem $tempOut -Filter *.mp3 | Sort-Object Name
if ($rippedFiles.Count -ne $TrackTitles.Count) {
    Write-Warning "Ripped $($rippedFiles.Count) file(s) but got $($TrackTitles.Count) title(s) - check the match below before trusting the renames."
}

for ($i = 0; $i -lt $rippedFiles.Count; $i++) {
    $title = if ($i -lt $TrackTitles.Count) { $TrackTitles[$i] } else { "Track $($i + 1)" }
    $safeTitle = ($title -replace '[\\/:*?"<>|]', '')
    $safeArtist = ($Artist -replace '[\\/:*?"<>|]', '')
    $dest = Join-Path $LibraryRoot "$safeArtist - $safeTitle.mp3"
    Copy-Item $rippedFiles[$i].FullName $dest -Force
    Write-Host "  -> $dest" -ForegroundColor Green
}

Remove-Item $tempOut -Recurse -Force
Write-Host "Done - open the Karaoke app's Remote page and the CD-ripninger tab should show these." -ForegroundColor Green

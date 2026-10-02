# ITMartin Rip CD (gratis udgave) - læg en musik-cd i, få den som FLAC eller MP3 med rigtige navne og cover.
# Bygget på Martins egen rip-station (rip-tools\ripstation.ps1 + finish-cd.ps1), uden NAS, nøgler og stier.
# Kører i Windows PowerShell 5.1 (findes på alle Windows 10/11). Gem filen som UTF-8 MED BOM, ellers bliver æøå forkerte.
# Værktøjer: fre:ac (ripper) og ffmpeg (tags, cover, mp3) - installeres med winget første gang.
# Kun musik-cd'er: dvd/blu-ray har kopibeskyttelse, og den må man ikke bryde (ophavsretsloven §75c).

$ErrorActionPreference = 'Stop'
$Version = '1.0'
$Ua = "ITMartinRipCD/$Version (ITMartin@Mensa.dk)"   # MusicBrainz vil have en kontakt i User-Agent
$AppDir = Join-Path $env:APPDATA 'ITMartin\RipCD'
$WorkRoot = Join-Path $env:LOCALAPPDATA 'ITMartin\RipCD\arbejde'
$SettingsFile = Join-Path $AppDir 'indstillinger.json'
$LogFile = Join-Path $AppDir 'log.txt'
New-Item -ItemType Directory -Force $AppDir, $WorkRoot | Out-Null
[Console]::OutputEncoding = [Text.Encoding]::UTF8
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Host.UI.RawUI.WindowTitle = 'ITMartin Rip CD'

function Log([string]$m) { Add-Content -LiteralPath $LogFile -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $m" -Encoding UTF8 }
function Say([string]$m, [string]$c = 'Gray') { Write-Host $m -ForegroundColor $c; Log $m }

# fre:ac ser kun cd-drevet, når den kører som administrator - start forfra som administrator.
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    exit
}

function Refresh-Path { $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User') }

function Find-Freac {
    $c = @(Get-Command freaccmd.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    $c += @(Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\enzo1982.freac*", "$env:ProgramFiles\freac*", "${env:ProgramFiles(x86)}\freac*" -Recurse -Filter freaccmd.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    $c | Select-Object -First 1
}
function Find-Ffmpeg {
    $c = @(Get-Command ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    $c += @(Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    $c | Select-Object -First 1
}

function Install-Tool([string]$id, [string]$name) {
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        Say "Kan ikke installere ${name}: winget mangler. Installér 'App Installer' fra Microsoft Store og start igen." Red
        Read-Host 'Tryk Enter for at lukke'; exit 1
    }
    Say "Installerer $name (kun første gang, tager et minut) ..." Cyan
    & winget.exe install --id $id -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
    Refresh-Path
}

function Load-Settings {
    if (Test-Path $SettingsFile) { try { return Get-Content $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { } }
    $null
}

function Ask-Settings {
    Write-Host ''
    Write-Host 'Hvilket format vil du have?' -ForegroundColor Cyan
    Write-Host '  1  MP3  - spiller på alt (telefon, bil, gamle anlæg). Mindre filer.'
    Write-Host '  2  FLAC - samme lyd som cd''en, større filer. Til hi-fi og Jellyfin/Plex.'
    $f = if ((Read-Host 'Vælg 1 eller 2 [1]') -eq '2') { 'flac' } else { 'mp3' }
    $def = [Environment]::GetFolderPath('MyMusic')
    $o = Read-Host "Hvor skal musikken ligge? [$def]"
    if (-not $o) { $o = $def }
    $s = [pscustomobject]@{ Format = $f; Folder = $o }
    $s | ConvertTo-Json | Set-Content $SettingsFile -Encoding UTF8
    $s
}

# --- MusicBrainz: rigtige navne, år og numre (cd'ens egen CDDB-gæt er tit forkert, fx "Various artists") ---
function MB-Get([string]$uri) {
    for ($i = 1; ; $i++) {
        try { return Invoke-RestMethod -Uri $uri -Headers @{ 'User-Agent' = $Ua } -TimeoutSec 20 }
        catch {
            if ($i -ge 4 -or "$($_.Exception.Message)" -notmatch '503') { throw }
            Start-Sleep -Seconds (3 * $i)   # MusicBrainz har travlt - vent og prøv igen
        }
    }
}

function Find-Release([string]$artist, [string]$album, [int]$tracks) {
    try {
        $q = [uri]::EscapeDataString("release:`"$album`" AND artist:`"$artist`"")
        $r = MB-Get "https://musicbrainz.org/ws/2/release/?query=$q&fmt=json&limit=10"
        $c = @($r.releases | Where-Object { $_.score -ge 70 -and $_.'track-count' -eq $tracks })
        if ($c.Count -eq 0) { $c = @($r.releases | Where-Object { $_.score -ge 90 }) }
        if ($c.Count -eq 0) { return $null }
        $best = $c | Sort-Object -Property @{ e = { if ($_.status -eq 'Official') { 0 } else { 1 } } }, @{ e = { if ($_.date) { 0 } else { 1 } } } | Select-Object -First 1
        Start-Sleep -Milliseconds 1100   # højst 1 kald i sekundet
        $rel = MB-Get "https://musicbrainz.org/ws/2/release/$($best.id)?inc=recordings+artist-credits&fmt=json"
        $titles = @(); foreach ($m in $rel.media) { foreach ($t in $m.tracks) { $titles += $t.title } }
        [pscustomobject]@{
            Id     = $rel.id
            Artist = ($rel.'artist-credit' | ForEach-Object { $_.name + $_.joinphrase }) -join ''
            Album  = $rel.title
            Year   = if ($rel.date) { $rel.date.Substring(0, 4) } else { $null }
            Titles = $titles
        }
    } catch { Log "MusicBrainz fejl: $($_.Exception.Message)"; $null }
}

function Get-Cover([string]$releaseId, [string]$to) {
    try {
        Invoke-WebRequest -Uri "https://coverartarchive.org/release/$releaseId/front-500" -OutFile $to -Headers @{ 'User-Agent' = $Ua } -TimeoutSec 30 -UseBasicParsing | Out-Null
        return (Test-Path $to) -and ((Get-Item $to).Length -gt 1000)
    } catch { return $false }
}

function Safe-Name([string]$s) { ($s -replace '\s*:\s*', ' - ' -replace '[\\/*?"<>|]', '_').Trim().TrimEnd('.') }

# --- Cd-drevet ---
function Get-Drive { Get-CimInstance Win32_CDROMDrive | Select-Object -First 1 -ExpandProperty Drive }
function Has-AudioCd([string]$d) { [bool](Get-ChildItem "$d\" -Filter *.cda -ErrorAction SilentlyContinue) }
function Eject([string]$d) {
    try { (New-Object -ComObject Shell.Application).NameSpace(17).ParseName($d).InvokeVerb('Eject') } catch { }
}

# --- Én cd: rip -> navne + cover -> færdige filer -> skuffen ud ---
function Rip-One([string]$drive, $s) {
    $work = Join-Path $WorkRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
    New-Item -ItemType Directory -Force $work | Out-Null
    Say "Ripper cd'en ... (ca. 3-8 minutter)" Cyan
    & $script:Freac --cddb -cd 0 -t all -e flac -d $work -p '<albumartist>\<album>\<track> <title>' *>> $LogFile
    $albums = @(Get-ChildItem $work -Directory | Get-ChildItem -Directory | Where-Object { Get-ChildItem -LiteralPath $_.FullName -Filter *.flac })
    if ($albums.Count -eq 0) { throw 'fre:ac lavede ingen filer - er cd''en ridset eller beskidt?' }
    Eject $drive   # næste cd kan komme i, mens vi gør den her færdig

    foreach ($albumDir in $albums) {
        $artist = $albumDir.Parent.Name; $album = $albumDir.Name
        $files = @(Get-ChildItem -LiteralPath $albumDir.FullName -Filter *.flac | Sort-Object Name)
        $year = $null; $titles = $null; $cover = $null
        Say "Slår op på MusicBrainz: $artist - $album"
        $rel = Find-Release $artist $album $files.Count
        if ($rel) {
            $artist = $rel.Artist; $album = $rel.Album; $year = $rel.Year
            if ($rel.Titles.Count -eq $files.Count) { $titles = $rel.Titles }
            $cover = Join-Path $albumDir.FullName 'folder.jpg'
            if (-not (Get-Cover $rel.Id $cover)) { $cover = $null }
        } else { Say '  Ikke fundet - beholder navnene fra cd''en.' Yellow }

        $dst = Join-Path $s.Folder (Join-Path (Safe-Name $artist) (Safe-Name $album))
        New-Item -ItemType Directory -Force $dst | Out-Null
        $i = 0
        foreach ($f in $files) {
            $num = $f.BaseName.Split(' ')[0]; $title = $f.BaseName.Substring($num.Length).Trim()
            if ($titles) { $title = $titles[$i] }
            $i++
            $meta = @('-metadata', "title=$title", '-metadata', "artist=$artist", '-metadata', "album_artist=$artist", '-metadata', "album=$album", '-metadata', "track=$([int]$num)/$($files.Count)")
            if ($year) { $meta += @('-metadata', "date=$year") }
            $out = Join-Path $dst "$num $(Safe-Name $title).$($s.Format)"
            $in = @('-i', $f.FullName); $map = @('-map', '0:a')
            if ($cover) { $in += @('-i', $cover); $map += @('-map', '1:v', '-disposition:v', 'attached_pic') }
            $codec = if ($s.Format -eq 'mp3') { @('-c:a', 'libmp3lame', '-q:a', '0', '-id3v2_version', '3', '-c:v', 'mjpeg') } else { @('-c', 'copy') }
            & $script:Ffmpeg -loglevel error -y @in @map @codec @meta $out
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $out)) { throw "ffmpeg kunne ikke lave $out" }
        }
        if ($cover) { Copy-Item $cover (Join-Path $dst 'folder.jpg') -Force }
        $made = @(Get-ChildItem -LiteralPath $dst -Filter "*.$($s.Format)").Count
        if ($made -lt $files.Count) { throw "Kun $made af $($files.Count) numre blev lavet" }
        Say "FÆRDIG: $artist - $album ($made numre$(if ($cover) { ', med cover' }))" Green
        Say "        $dst" Green
    }
    Remove-Item $work -Recurse -Force   # først når alle numre er lavet - ellers bliver rippet liggende til næste forsøg
}

# --- Start ---
Clear-Host
Write-Host "ITMartin Rip CD $Version - gratis udgave" -ForegroundColor Cyan
Write-Host 'Læg en musik-cd i. Når den er færdig, kommer skuffen ud - så lægger du den næste i.'
Write-Host 'Luk vinduet, når du er færdig.'
Write-Host 'Kun CD''er du selv ejer - til dig og din husstand. Ikke lånte CD''er, ikke deling. Se mineskiver.itmartin.dk/regler' -ForegroundColor DarkYellow

Refresh-Path
$script:Freac = Find-Freac;   if (-not $script:Freac)  { Install-Tool 'enzo1982.freac' 'fre:ac (cd-ripper)'; $script:Freac = Find-Freac }
$script:Ffmpeg = Find-Ffmpeg; if (-not $script:Ffmpeg) { Install-Tool 'Gyan.FFmpeg' 'ffmpeg (lyd og cover)'; $script:Ffmpeg = Find-Ffmpeg }
if (-not $script:Freac -or -not $script:Ffmpeg) { Say 'Værktøjerne kunne ikke installeres. Se log.txt i %APPDATA%\ITMartin\RipCD.' Red; Read-Host 'Tryk Enter for at lukke'; exit 1 }

$s = Load-Settings
if (-not $s) { $s = Ask-Settings }
else {
    Write-Host "Format: $($s.Format.ToUpper())   Mappe: $($s.Folder)"
    if ((Read-Host 'Tryk Enter for at fortsætte, eller skriv  ny  for at ændre') -eq 'ny') { $s = Ask-Settings }
}
New-Item -ItemType Directory -Force $s.Folder | Out-Null

$drive = Get-Drive
if (-not $drive) { Say 'Fandt intet cd-drev. Sæt et USB-cd-drev i og start igen.' Red; Read-Host 'Tryk Enter for at lukke'; exit 1 }
Say "Cd-drev: $drive"

$count = 0
while ($true) {
    Write-Host ''
    Write-Host "Venter på en musik-cd i $drive ..." -ForegroundColor DarkGray
    while (-not (Has-AudioCd $drive)) { Start-Sleep -Seconds 3 }
    try { Rip-One $drive $s; $count++; Say "Cd'er i dag: $count" Cyan }
    catch { Say "FEJL: $($_.Exception.Message)" Red; Eject $drive }
    # vent til skuffen er tom, så den samme cd ikke rippes to gange
    while (Has-AudioCd $drive) { Start-Sleep -Seconds 3 }
}

# ITMartin Rip CD (gratis udgave) - læg en musik-cd i, få den som FLAC eller MP3 med rigtige navne og cover.
# Bygget på Martins egen rip-station (rip-tools\ripstation.ps1 + finish-cd.ps1), uden NAS, nøgler og stier.
# Kører i Windows PowerShell 5.1 (findes på alle Windows 10/11). Gem filen som UTF-8 MED BOM, ellers bliver æøå forkerte.
# Værktøjer: fre:ac (ripper) og ffmpeg (tags, cover, mp3) - installeres med winget første gang.
# Kun musik-cd'er: dvd/blu-ray har kopibeskyttelse, og den må man ikke bryde (ophavsretsloven §75c).

$ErrorActionPreference = 'Stop'
$Version = '1.2'
$Ua = "ITMartinRipCD/$Version (ITMartin@Mensa.dk)"   # MusicBrainz vil have en kontakt i User-Agent
$AppDir = Join-Path $env:APPDATA 'ITMartin\RipCD'
$WorkRoot = Join-Path $env:LOCALAPPDATA 'ITMartin\RipCD\arbejde'
$SettingsFile = Join-Path $AppDir 'indstillinger.json'
$LogFile = Join-Path $AppDir 'log.txt'
$SentFile = Join-Path $AppDir 'sendt.txt'      # albummapper, der er sendt til Martin (én pr. linje)
$ProgDir = Join-Path $AppDir 'program'          # her bor programmet, så ikonet på skrivebordet altid virker
# Kundens egen udgave (hentet fra Bibliotek) har send-til.txt med upload-linket: så spørges der ikke om noget,
# og hvert album sendes af sig selv til kundens samling. Den gratis udgave har ingen fil og opfører sig som før.
$SendTo = $null
$sendFile = Join-Path $PSScriptRoot 'send-til.txt'
if (Test-Path -LiteralPath $sendFile) { $SendTo = (Get-Content -LiteralPath $sendFile -Encoding UTF8 | Where-Object { $_ -match '^https://' } | Select-Object -First 1) }
New-Item -ItemType Directory -Force $AppDir, $WorkRoot | Out-Null
[Console]::OutputEncoding = [Text.Encoding]::UTF8
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Host.UI.RawUI.WindowTitle = 'ITMartin Rip CD'

function Log([string]$m) { Add-Content -LiteralPath $LogFile -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $m" -Encoding UTF8 }
function Say([string]$m, [string]$c = 'Gray') { Write-Host $m -ForegroundColor $c; Log $m }

# fre:ac ser kun cd-drevet, når den kører som administrator - start forfra som administrator.
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    # Uden besked her blinker vinduet bare og forsvinder - og spørgsmålet gemmer sig tit i proceslinjen.
    Write-Host 'Windows spørger nu, om Rip CD må køre som administrator - tryk Ja.' -ForegroundColor Cyan
    Write-Host 'Kan du ikke se spørgsmålet, så klik på det, der blinker orange nede i proceslinjen.'
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"") -ErrorAction Stop
        Write-Host 'Rip CD åbner i et nyt vindue. Dette vindue lukker om lidt.'
        Start-Sleep -Seconds 6
    } catch {
        Write-Host 'Rip CD fik ikke lov (der blev trykket Nej). Start den igen, og tryk Ja.' -ForegroundColor Red
        Read-Host 'Tryk Enter for at lukke'
    }
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

# Navne fra fre:ac's CDDB-opslag ligger som tags i filen. Tomme felter = cd'en blev ikke fundet.
function Read-Tags([string]$file) {
    $probe = Join-Path (Split-Path $script:Ffmpeg) 'ffprobe.exe'
    if (-not (Test-Path $probe)) { return @{} }
    try {
        $j = & $probe -v error -show_entries format_tags -of json $file | Out-String | ConvertFrom-Json
        $t = @{}
        foreach ($p in $j.format.tags.PSObject.Properties) { $t[$p.Name.ToLower()] = "$($p.Value)".Trim() }
        $t
    } catch { @{} }
}
function Known([string]$v) { $v -and $v -notmatch '^(unknown|ukendt)' }

# --- Én cd: rip -> navne + cover -> færdige filer -> skuffen ud ---
# Ét spor ad gangen: på nogle pc'er laver fre:ac kun spor 1, når den skal tage hele cd'en i ét kald
# ("Could not process file" på resten). Hvert spor får to forsøg.
function Rip-One([string]$drive, $s) {
    $work = Join-Path $WorkRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
    New-Item -ItemType Directory -Force $work | Out-Null
    $n = @(Get-ChildItem "$drive\" -Filter *.cda -ErrorAction SilentlyContinue).Count
    Say "Ripper cd'en: $n numre (ca. 3-8 minutter i alt)" Cyan
    $missing = @()
    for ($t = 1; $t -le $n; $t++) {
        $f = Join-Path $work ('{0:D2}.flac' -f $t)
        Write-Host "  Nummer $t af $n ..." -NoNewline
        for ($try = 1; $try -le 2 -and -not (Test-Path -LiteralPath $f); $try++) {
            Log "fre:ac nummer $t forsøg $try"
            & $script:Freac --cddb -cd 0 -t $t -e flac -o $f *>> $LogFile
        }
        if (Test-Path -LiteralPath $f) { Write-Host ' ok' -ForegroundColor Green }
        else { Write-Host ' kunne ikke læses' -ForegroundColor Red; $missing += $t }
    }
    $files = @(Get-ChildItem -LiteralPath $work -Filter *.flac | Sort-Object Name)
    if ($files.Count -eq 0) { throw 'fre:ac lavede ingen filer - er cd''en ridset eller beskidt?' }
    Eject $drive   # næste cd kan komme i, mens vi gør den her færdig

    $tags = Read-Tags $files[0].FullName
    $artist = if (Known $tags['album_artist']) { $tags['album_artist'] } elseif (Known $tags['artist']) { $tags['artist'] } else { $null }
    $album = if (Known $tags['album']) { $tags['album'] } else { $null }
    $year = $null; $titles = $null; $cover = $null
    $rel = $null
    if ($artist -and $album) {
        Say "Slår op på MusicBrainz: $artist - $album"
        $rel = Find-Release $artist $album $n
    }
    if ($rel) {
        $artist = $rel.Artist; $album = $rel.Album; $year = $rel.Year
        if ($rel.Titles.Count -eq $n) { $titles = $rel.Titles }
        $cover = Join-Path $work 'folder.jpg'
        if (-not (Get-Cover $rel.Id $cover)) { $cover = $null }
    } elseif ($artist -and $album) { Say '  Ikke fundet på MusicBrainz - beholder navnene fra cd''en.' Yellow }
    else {
        $artist = 'Ukendt kunstner'; $album = "Ukendt cd $(Get-Date -Format 'yyyy-MM-dd HH.mm')"
        Say "  Cd'en blev ikke fundet - gemt som '$album'. Du kan selv omdøbe mappen bagefter." Yellow
    }

    $dst = Join-Path $s.Folder (Join-Path (Safe-Name $artist) (Safe-Name $album))
    New-Item -ItemType Directory -Force $dst | Out-Null
    foreach ($f in $files) {
        $num = $f.BaseName
        $tn = [int]$num
        $title = (Read-Tags $f.FullName)['title']
        if (-not (Known $title)) { $title = "Nummer $tn" }
        if ($titles) { $title = $titles[$tn - 1] }
        $meta = @('-metadata', "title=$title", '-metadata', "artist=$artist", '-metadata', "album_artist=$artist", '-metadata', "album=$album", '-metadata', "track=$tn/$n")
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
    if ($missing) {
        Say "Nummer $($missing -join ', ') kunne ikke læses. Tør cd'en af (fra midten og ud) og læg den i igen." Red
        Say "Rippet ligger stadig i $work" Red
        return
    }
    Say "FÆRDIG: $artist - $album ($made numre$(if ($cover) { ', med cover' }))" Green
    Say "        $dst" Green
    Remove-Item $work -Recurse -Force   # først når alle numre er lavet - ellers bliver rippet liggende til næste forsøg
    if ($SendTo) { [void](Send-Album $dst) }
}

# --- Ikon på skrivebordet: kopiér programmet til %APPDATA% og lav "Rip CD", der starter som administrator ---
# (så kommer Windows' spørgsmål med det samme, i stedet for at vinduet blinker og gemmer det i proceslinjen)
function Install-Shortcut {
    try {
        if ($PSScriptRoot -ne $ProgDir) {
            New-Item -ItemType Directory -Force $ProgDir | Out-Null
            Get-ChildItem -LiteralPath $PSScriptRoot -File | Copy-Item -Destination $ProgDir -Force
        }
        $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Rip CD.lnk'
        if (Test-Path -LiteralPath $lnk) { return }
        $sh = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
        $sh.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $sh.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $ProgDir 'RipCD.ps1')`""
        $sh.WorkingDirectory = $ProgDir
        $sh.IconLocation = Join-Path $env:WINDIR 'System32\imageres.dll,25'
        $sh.Description = 'ITMartin Rip CD - læg en musik-cd i'
        $sh.Save()
        $b = [IO.File]::ReadAllBytes($lnk); $b[0x15] = $b[0x15] -bor 0x20; [IO.File]::WriteAllBytes($lnk, $b)   # "Kør som administrator"
        Say 'Der er nu et ikon på skrivebordet: Rip CD. Næste gang dobbeltklikker du bare på det.' Green
    } catch { Log "Ikon fejl: $($_.Exception.Message)" }
}

# --- Send et album til kundens samling (upload-boksen hos Martin), mappe for mappe ---
function Sent-Albums { if (Test-Path -LiteralPath $SentFile) { @(Get-Content -LiteralPath $SentFile -Encoding UTF8) } else { @() } }

function Send-Album([string]$dir) {
    $album = Split-Path $dir -Leaf; $artist = Split-Path (Split-Path $dir -Parent) -Leaf
    $api = ($SendTo -replace '/upload/', '/api/upload/')
    $files = @(Get-ChildItem -LiteralPath $dir -File | Where-Object { $_.Extension -in '.flac', '.mp3', '.jpg' })
    if ($files.Count -eq 0) { return $true }
    Say "Sender $artist - $album til din samling ($($files.Count) filer) ..." Cyan
    Add-Type -AssemblyName System.Net.Http
    $http = New-Object System.Net.Http.HttpClient
    $http.Timeout = [TimeSpan]::FromMinutes(10)
    try {
        foreach ($f in $files) {
            $ok = $false
            for ($try = 1; $try -le 3 -and -not $ok; $try++) {
                $stream = [IO.File]::OpenRead($f.FullName)
                try {
                    $form = New-Object System.Net.Http.MultipartFormDataContent
                    $form.Add((New-Object System.Net.Http.StringContent("$artist/$album")), 'path')
                    $content = New-Object System.Net.Http.StreamContent($stream)
                    $form.Add($content, 'file', $f.Name)
                    $r = $http.PostAsync($api, $form).GetAwaiter().GetResult()
                    $ok = $r.IsSuccessStatusCode
                    if (-not $ok) { Log "Upload $($f.Name): $([int]$r.StatusCode)" }
                } catch { Log "Upload $($f.Name) forsøg ${try}: $($_.Exception.Message)" }
                finally { $stream.Dispose() }
                if (-not $ok) { Start-Sleep -Seconds (5 * $try) }
            }
            if (-not $ok) {
                Say "  Kunne ikke sende $($f.Name) - tjek internettet. Det prøves igen, næste gang du starter Rip CD." Yellow
                return $false
            }
        }
    } finally { $http.Dispose() }
    Add-Content -LiteralPath $SentFile -Value $dir -Encoding UTF8
    Say "  Sendt. Om ca. en halv time står det i din samling i Bibliotek." Green
    $true
}

# Albums, der ikke kom af sted endnu (internettet var nede - eller rippet med en ældre Rip CD).
# Kun FLAC-albummer med "01 ..."-navne: det er dem, Rip CD laver; anden musik i mappen røres ikke.
function Send-Pending($s) {
    if (-not $SendTo) { return }
    $sent = Sent-Albums
    foreach ($a in @(Get-ChildItem -LiteralPath $s.Folder -Directory -ErrorAction SilentlyContinue | Get-ChildItem -Directory -ErrorAction SilentlyContinue)) {
        if ($sent -contains $a.FullName) { continue }
        $ripped = @(Get-ChildItem -LiteralPath $a.FullName -File | Where-Object { $_.Name -match "^\d\d .+\.$($s.Format)$" })
        if ($ripped.Count -gt 0) { [void](Send-Album $a.FullName) }
    }
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
if (-not $s -and $SendTo) {
    # Kundens udgave: FLAC (samme lyd som cd'en) i mappen Musik - ingen spørgsmål.
    $s = [pscustomobject]@{ Format = 'flac'; Folder = [Environment]::GetFolderPath('MyMusic') }
    $s | ConvertTo-Json | Set-Content $SettingsFile -Encoding UTF8
}
if (-not $s) { $s = Ask-Settings }
elseif ($SendTo) { Write-Host "Musikken gemmes i $($s.Folder) og sendes til din samling i Bibliotek." }
else {
    Write-Host "Format: $($s.Format.ToUpper())   Mappe: $($s.Folder)"
    if ((Read-Host 'Tryk Enter for at fortsætte, eller skriv  ny  for at ændre') -eq 'ny') { $s = Ask-Settings }
}
New-Item -ItemType Directory -Force $s.Folder | Out-Null
Install-Shortcut
Send-Pending $s

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

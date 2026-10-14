# R6Replays.ps1 - sends the stats from Siege's own replay files to r6.itmartin.dk.
# Siege saves every match in ...\Tom Clancy's Rainbow Six Siege\MatchReplay\Match-<date>\*.rec (one file per round)
# when "Match Replay" is on in the game's settings. This script reads them with r6-dissect (open source,
# github.com/redraskal/r6-dissect, downloaded once to %LOCALAPPDATA%\R6Assistant and checked against its MD5) and
# sends score, kills, deaths and operators per round. It reads only - nothing in the game folder is changed.
# First run sends every match on the PC; after that it keeps watching and sends each new match when it is over.
# -Server / -DataDir are only for testing against a local copy of the app.
param([switch]$Once, [string]$Server = "https://r6.itmartin.dk", [string]$DataDir = "")

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Home6    = if ($DataDir) { $DataDir } else { Join-Path $env:LOCALAPPDATA "R6Assistant" }
$ToolDir  = Join-Path $Home6 "r6-dissect"
$Tool     = Join-Path $ToolDir "r6-dissect.exe"
$SentFile = Join-Path $Home6 "sent-replays.txt"
$NameFile = Join-Path $Home6 "player.txt"
$ZipUrl   = "https://github.com/redraskal/r6-dissect/releases/download/v0.24.0/r6-dissect-v0.24.0-windows-amd64.zip"
$ZipMd5   = "6B539FF2CEF1AF61F3ED63A7D16AE712"
New-Item -ItemType Directory -Force $Home6 | Out-Null

Write-Host "R6 kampstatistik fra replays" -ForegroundColor Yellow
Write-Host "Læser Siege's replay-filer og sender tallene til $Server. Luk vinduet, når I er færdige med at spille."
Write-Host ""

# Player name - the same file PreGameCheck uses.
if (Test-Path $NameFile) { $Player = (Get-Content $NameFile -Raw).Trim() }
if (-not $Player) {
    $Player = Read-Host "Dit navn i holdet (fx Martin)"
    Set-Content -Path $NameFile -Value $Player -Encoding UTF8
}

# r6-dissect: download once, check MD5, unpack.
if (-not (Test-Path $Tool)) {
    Write-Host "Henter r6-dissect (9 MB, kun første gang) ..."
    $zip = Join-Path $env:TEMP "r6-dissect.zip"
    Invoke-WebRequest -UseBasicParsing $ZipUrl -OutFile $zip
    $md5 = (Get-FileHash $zip -Algorithm MD5).Hash
    if ($md5 -ne $ZipMd5) { Remove-Item $zip; throw "r6-dissect download er ikke den forventede fil (MD5 $md5). Stopper." }
    Expand-Archive $zip -DestinationPath $ToolDir -Force
    Remove-Item $zip
}

# Where Siege keeps its replays: Ubisoft Connect install from the registry, then the usual folders.
function Find-ReplayFolders {
    $roots = @()
    foreach ($key in "HKLM:\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\635", "HKLM:\SOFTWARE\Ubisoft\Launcher\Installs\635") {
        try { $roots += (Get-ItemProperty $key -ErrorAction Stop).InstallDir } catch {}
    }
    $roots += "${env:ProgramFiles(x86)}\Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege"
    $roots += "${env:ProgramFiles(x86)}\Steam\steamapps\common\Tom Clancy's Rainbow Six Siege"
    foreach ($d in (Get-PSDrive -PSProvider FileSystem).Root) {
        $roots += Join-Path $d "SteamLibrary\steamapps\common\Tom Clancy's Rainbow Six Siege"
        $roots += Join-Path $d "Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege"
    }
    $roots | Where-Object { $_ } | ForEach-Object { Join-Path $_ "MatchReplay" } | Where-Object { Test-Path $_ } | Select-Object -Unique
}

function Send-Match($folder) {
    $recs = Get-ChildItem $folder.FullName -Filter *.rec | Sort-Object Name
    if ($recs.Count -eq 0) { return $true }
    $tmp = Join-Path $env:TEMP "r6round.json"
    $rounds = @()
    foreach ($rec in $recs) {
        if (Test-Path $tmp) { Remove-Item $tmp }
        # r6-dissect crashes on rounds with operators newer than the tool ("role unknown for operator ID") - those
        # rounds are skipped, the rest still count. Own process, so its crash text cannot stop this script.
        $p = Start-Process -FilePath $Tool -ArgumentList "`"$($rec.FullName)`"", "-o", "`"$tmp`"" -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput "$tmp.out" -RedirectStandardError "$tmp.err"
        if ($p.ExitCode -eq 0 -and (Test-Path $tmp) -and (Get-Item $tmp).Length -gt 0) {
            $rounds += [IO.File]::ReadAllText($tmp, [Text.Encoding]::UTF8)
        }
    }
    if ($rounds.Count -eq 0) { Write-Host "  $($folder.Name): ingen runder kunne læses" -ForegroundColor DarkYellow; return $true }
    $name = $Player.Replace('\', '\\').Replace('"', '\"')
    $body = '{"spiller":"' + $name + '","folder":"' + $folder.Name + '","totalRounds":' + $recs.Count + ',"rounds":[' + ($rounds -join ',') + ']}'
    try {
        $resp = Invoke-RestMethod -Uri "$Server/api/replay/import" -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType "application/json; charset=utf-8" -TimeoutSec 60
        Write-Host "  $($folder.Name): $resp" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  $($folder.Name): kunne ikke sendes ($($_.Exception.Message)) - prøver igen senere" -ForegroundColor Red
        return $false
    }
}

$folders = @(Find-ReplayFolders)
if ($folders.Count -eq 0) {
    Write-Host "Fandt ingen MatchReplay-mappe." -ForegroundColor Red
    Write-Host "Slå replays til i Siege: Options / Indstillinger -> General / Generelt -> Match Replay -> On. Spil en kamp, og kør så scriptet igen."
    return
}
Write-Host "Replay-mappe: $($folders -join ', ')"

do {
    $sent = @(); if (Test-Path $SentFile) { $sent = Get-Content $SentFile }
    foreach ($root in $folders) {
        $new = Get-ChildItem $root -Directory -Filter "Match-*" | Where-Object { $sent -notcontains $_.Name } | Sort-Object Name
        foreach ($m in $new) {
            # A match still being played keeps writing files - wait until nothing has changed for 3 minutes.
            $last = (Get-ChildItem $m.FullName -File | Measure-Object LastWriteTime -Maximum).Maximum
            if ($last -and $last -gt (Get-Date).AddMinutes(-3)) { continue }
            if (Send-Match $m) { Add-Content -Path $SentFile -Value $m.Name }
        }
    }
    if (-not $Once) {
        Write-Host ("[{0:HH:mm}] Venter på næste kamp ... (luk vinduet for at stoppe)" -f (Get-Date)) -ForegroundColor DarkGray
        Start-Sleep -Seconds 60
    }
} while (-not $Once)

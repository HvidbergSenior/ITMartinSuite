# PreGameCheck.ps1
# Run this ~2 minutes before an R6 Siege session. Gathers local system state
# (network, audio/Discord, game/launcher, system) and sends it to
# r6assistant-web, which asks Claude for a short checklist of what's fine and
# what to fix before you play. No admin rights required. Safe to share with
# teammates - it never touches your Claude API key, that lives on the server.
#
# Each check is wrapped so one failure doesn't kill the whole script - a
# missing value just gets skipped, not treated as an error.

# Windows PowerShell 5.1's console defaults to a legacy code page (not UTF-8)
# for THREE separate things, and all three need fixing or Danish text garbles
# somewhere along the way even though the script file and the API response are
# both plain UTF-8 the whole time:
#   1. chcp 65001         - the actual console code page (affects native
#                            commands like powercfg, which emits Danish text
#                            like "Hoej ydelse" using this code page)
#   2. Console.InputEncoding  - how PowerShell decodes that native output
#   3. Console.OutputEncoding - how PowerShell displays text back to you
& "$env:SystemRoot\System32\chcp.com" 65001 > $null
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$Endpoint = "https://r6.itmartin.dk/api/pregame/check"

function Try-Get {
    param([scriptblock]$Block, $Default = $null)
    try { & $Block } catch { $Default }
}

Write-Host "Indsamler systemtilstand..." -ForegroundColor Cyan

# ── Netvaerk ──────────────────────────────────────────────────────────────
$activeAdapter = Try-Get { Get-NetAdapter | Where-Object { $_.Status -eq "Up" } | Select-Object -First 1 }
$connectionType = if ($activeAdapter) {
    if ($activeAdapter.MediaType -match "802.11" -or $activeAdapter.InterfaceDescription -match "Wireless|Wi-Fi") { "Wi-Fi" } else { "Kablet (Ethernet)" }
} else { $null }

$ping = Try-Get {
    $result = Test-Connection -ComputerName 1.1.1.1 -Count 4 -ErrorAction Stop
    [PSCustomObject]@{
        AvgLatencyMs = [math]::Round(($result | Measure-Object -Property ResponseTime -Average).Average, 1)
        PacketLossPct = [math]::Round((4 - $result.Count) / 4 * 100, 0)
    }
}

$vpnActive = Try-Get {
    $vpnAdapters = Get-NetAdapter | Where-Object { $_.InterfaceDescription -match "VPN|TAP|WireGuard|Nord|Tailscale" -and $_.Status -eq "Up" }
    $vpnAdapters.Count -gt 0
} -Default $false

# ── Lyd & Discord ────────────────────────────────────────────────────────
$audioDevices = Try-Get {
    Get-CimInstance -ClassName Win32_SoundDevice | Select-Object -ExpandProperty Name
}

$discordProcess = Try-Get { Get-Process -Name "Discord" -ErrorAction Stop | Select-Object -First 1 }
$discordRunning = $null -ne $discordProcess
$discordVersion = Try-Get {
    if ($discordProcess) { $discordProcess.Path | ForEach-Object { (Get-Item $_).VersionInfo.ProductVersion } }
}

# ── Spil & Launcher ──────────────────────────────────────────────────────
$r6Path = Try-Get {
    $candidates = @(
        "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege",
        "C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege"
    )
    $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
$r6LastModified = Try-Get {
    if ($r6Path) {
        $exe = Get-ChildItem -Path $r6Path -Filter "RainbowSix*.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($exe) { $exe.LastWriteTime.ToString("yyyy-MM-dd") }
    }
}
$ubisoftConnectRunning = Try-Get { $null -ne (Get-Process -Name "UbisoftConnect" -ErrorAction Stop) } -Default $false
$battlEyeStatus = Try-Get { (Get-Service -Name "BEService" -ErrorAction Stop).Status.ToString() }

# ── System ───────────────────────────────────────────────────────────────
$gpu = Try-Get {
    Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.AdapterRAM -gt 0 } | Select-Object -First 1 |
        ForEach-Object { [PSCustomObject]@{ Name = $_.Name; DriverDate = $_.DriverDate; DriverVersion = $_.DriverVersion } }
}

$pendingReboot = Try-Get {
    Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
} -Default $false

$powerPlan = Try-Get {
    (powercfg /getactivescheme) -replace ".*\((.+)\)", '$1'
}

$freeDiskGB = Try-Get {
    [math]::Round((Get-PSDrive -Name C).Free / 1GB, 1)
}

$topProcesses = Try-Get {
    Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 -ExpandProperty ProcessName
}

# ── Byg payload ──────────────────────────────────────────────────────────
$payload = [PSCustomObject]@{
    netvaerk = [PSCustomObject]@{
        forbindelsestype = $connectionType
        ping_ms = $ping.AvgLatencyMs
        pakketab_pct = $ping.PacketLossPct
        vpn_aktiv = $vpnActive
    }
    lyd_discord = [PSCustomObject]@{
        lydenheder = $audioDevices
        discord_koerer = $discordRunning
        discord_version = $discordVersion
    }
    spil_launcher = [PSCustomObject]@{
        r6_fundet = $null -ne $r6Path
        r6_sidst_aendret = $r6LastModified
        ubisoft_connect_koerer = $ubisoftConnectRunning
        battleye_status = $battlEyeStatus
    }
    system = [PSCustomObject]@{
        gpu_navn = $gpu.Name
        gpu_driver_dato = $gpu.DriverDate
        genstart_afventer = $pendingReboot
        stroemplan = $powerPlan
        ledig_diskplads_gb = $freeDiskGB
        top_processer = $topProcesses
    }
}

$json = $payload | ConvertTo-Json -Depth 5

Write-Host "Sender til r6assistant-web..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $json -ContentType "application/json" -TimeoutSec 30
    Write-Host ""
    Write-Host "=== TJEKLISTE ===" -ForegroundColor Yellow
    Write-Host $response
}
catch {
    Write-Host ""
    Write-Host "Kunne ikke naa r6assistant-web ($Endpoint) - er den startet?" -ForegroundColor Red
    Write-Host "Her er de raa data i stedet:" -ForegroundColor Yellow
    Write-Host $json
}

# PreGameCheck.ps1
# Run this ~2 minutes before an R6 Siege session. Gathers local system state
# (network, audio/Discord, game/launcher, system) and sends it to
# r6assistant-web, which asks Claude for a short checklist of what's fine and
# what to fix before you play. No admin rights required. Safe to share with
# teammates - it never touches your Claude API key, that lives on the server.
# Asks for your name the first time (then remembers it locally) so your
# submission shows up under your name on the team's overview page.
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

# Asked once per machine, then remembered locally - so teammates can see
# whose setup is whose on the team overview page without typing a name every
# single run. Delete the file to be asked again (e.g. shared/borrowed PC).
$NameFile = "$env:LOCALAPPDATA\R6Assistant\player.txt"
$PlayerName = Try-Get { (Get-Content -Path $NameFile -ErrorAction Stop).Trim() }
if ([string]::IsNullOrWhiteSpace($PlayerName)) {
    $PlayerName = Read-Host "Dit navn (bruges på team-overblikket)"
    Try-Get {
        New-Item -ItemType Directory -Path (Split-Path $NameFile) -Force | Out-Null
        Set-Content -Path $NameFile -Value $PlayerName
    }
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

# Headset/audio-enhancement software (SteelSeries GG/Sonar, Logitech G HUB,
# Razer Synapse, Corsair iCUE, HyperX NGENUITY, EPOS, Turtle Beach, ASTRO) -
# each of these can quietly grab the mic/output device or add its own noise
# gate/EQ, which is a common, easy-to-miss cause of "sound breaks up every
# session" style problems the AI can flag if it knows one is running.
$headsetSoftwareMap = @{
    "SteelSeriesGG"          = "SteelSeries GG"
    "SteelSeriesSonarClient" = "SteelSeries Sonar"
    "LGHUB"                  = "Logitech G HUB"
    "RazerCentralService"    = "Razer Synapse"
    "Razer Synapse Service"  = "Razer Synapse"
    "iCUE"                   = "Corsair iCUE"
    "NGENUITY"               = "HyperX NGENUITY"
    "EPOSGamingSuite"        = "EPOS Gaming Suite"
    "Audio Hub"              = "Turtle Beach Audio Hub"
    "AstroCommandCenter"     = "ASTRO Command Center"
}
$runningHeadsetSoftware = Try-Get {
    $headsetSoftwareMap.Keys | ForEach-Object {
        if (Get-Process -Name $_ -ErrorAction SilentlyContinue) { $headsetSoftwareMap[$_] }
    }
} -Default @()

# ── Spil & Launcher ──────────────────────────────────────────────────────
$r6Path = Try-Get {
    $candidates = @(
        "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege",
        "C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege"
    )
    $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
$r6ExeLastWrite = Try-Get {
    if ($r6Path) {
        $exe = Get-ChildItem -Path $r6Path -Filter "RainbowSix*.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($exe) { $exe.LastWriteTime }
    }
}
$r6LastModified = if ($r6ExeLastWrite) { $r6ExeLastWrite.ToString("yyyy-MM-dd") } else { $null }
# A long gap here is exactly the "haven't played in months" case - the AI
# should flag it as the #1 priority, since the update itself can take a long
# time and people need to start it well before the group is ready to play.
$r6DageSidenOpdateret = if ($r6ExeLastWrite) { [math]::Round(((Get-Date) - $r6ExeLastWrite).TotalDays, 0) } else { $null }
$ubisoftConnectRunning = Try-Get { $null -ne (Get-Process -Name "UbisoftConnect" -ErrorAction Stop) } -Default $false
$battlEyeStatus = Try-Get { (Get-Service -Name "BEService" -ErrorAction Stop).Status.ToString() }

# ── System ───────────────────────────────────────────────────────────────
$gpu = Try-Get {
    Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.AdapterRAM -gt 0 } | Select-Object -First 1 |
        ForEach-Object { [PSCustomObject]@{ Name = $_.Name; DriverDate = $_.DriverDate; DriverVersion = $_.DriverVersion } }
}

# Best-effort hardware names - useful for "why does X's mouse feel different"
# style troubleshooting between teammates, not for anything precise.
# Win32_PointingDevice/Win32_Keyboard almost always just say "HID-compliant
# mouse"/"Enhanced (101-102 key)" - generic USB HID names, not the real
# product - kept as the reliable fallback for the Mus/Tastatur fields below.
$mouseName = Try-Get { Get-CimInstance -ClassName Win32_PointingDevice | Select-Object -First 1 -ExpandProperty Name }
$keyboardName = Try-Get { Get-CimInstance -ClassName Win32_Keyboard | Select-Object -First 1 -ExpandProperty Name }

# The real product name (e.g. "PRO WIRELESS" for a Logitech G Pro Wireless)
# sometimes only shows up elsewhere in the Plug-and-Play device tree - e.g.
# as a sub-device for RGB lighting - not attributable back to specifically
# "the mouse" vs "the keyboard" with any confidence, so this is reported
# alongside the two generic fields above rather than replacing either one.
$knownPeripheralVendorVids = "046D|1532|1038|1B1C|0951|03F0|0B05|248A"  # Logitech, Razer, SteelSeries, Corsair, HyperX, ASUS, Attack Shark
$brandedNameExcludePattern = '^(HID-compliant|USB Input Device|USB Composite Device|LIGHTSPEED Receiver|HID Keyboard Device|HID Mouse|Generic|Bluetooth)|Virtual'
$brandedPeripherals = Try-Get {
    Get-PnpDevice -Status OK -ErrorAction Stop |
        Where-Object { $_.InstanceId -match "VID_($knownPeripheralVendorVids)" -and $_.FriendlyName -and $_.FriendlyName -notmatch $brandedNameExcludePattern } |
        Select-Object -ExpandProperty FriendlyName -Unique
} -Default @()
$pcModel = Try-Get {
    $cs = Get-CimInstance -ClassName Win32_ComputerSystem
    "$($cs.Manufacturer) $($cs.Model)".Trim()
}
$cpuName = Try-Get { Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1 -ExpandProperty Name }
$ramGB = Try-Get { [math]::Round((Get-CimInstance -ClassName Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 0) }
$osVersion = Try-Get { (Get-CimInstance -ClassName Win32_OperatingSystem).Caption }

# Resolution + refresh rate straight from the video controller - more
# reliable than asking the browser (which can't see refresh rate at all,
# and reports the browser window's monitor, not necessarily "the" monitor
# on a multi-monitor setup where R6 actually runs).
$screenInfo = Try-Get {
    $vc = Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution } | Select-Object -First 1
    if ($vc) { "$($vc.CurrentHorizontalResolution)x$($vc.CurrentVerticalResolution) @ $($vc.CurrentRefreshRate)Hz" }
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

# A PC idle for months (or just never rebooted) can be sitting on stuck
# updates or stale driver state - a fresh reboot before playing is a cheap,
# classic fix that's especially worth flagging after a long pause.
$dageSidenGenstart = Try-Get {
    [math]::Round(((Get-Date) - (Get-CimInstance -ClassName Win32_OperatingSystem).LastBootUpTime).TotalDays, 0)
}

$topProcesses = Try-Get {
    Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 -ExpandProperty ProcessName
}

function Get-RecentLogEvents {
    param([string]$LogName, [string]$ProviderPattern, [int]$Max = 10)
    Try-Get {
        Get-WinEvent -FilterHashtable @{ LogName = $LogName; Level = 1, 2, 3; StartTime = (Get-Date).AddDays(-3) } -MaxEvents 300 -ErrorAction Stop |
            Where-Object { $_.ProviderName -match $ProviderPattern -or $_.Message -match $ProviderPattern } |
            Select-Object -First $Max -Property @(
                @{ n = 'tid'; e = { $_.TimeCreated.ToString("yyyy-MM-dd HH:mm") } },
                @{ n = 'kilde'; e = { $_.ProviderName } },
                @{ n = 'besked'; e = { $m = ($_.Message -replace '\s+', ' '); $m.Substring(0, [Math]::Min(150, $m.Length)) } }
            )
    } -Default @()
}

# WHEA-Logger + GPU driver timeout/recovery events (System log) cover
# hardware-level causes of a mid-game stutter/freeze (PCIe bus errors, GPU
# faults, memory ECC issues, "Display driver stopped responding and has
# recovered"). Application log adds R6 Siege's own crashes/hangs. Capped
# small and truncated per entry - this is context for the AI, not a log dump.
$recentSystemErrors = Get-RecentLogEvents -LogName "System" -ProviderPattern "WHEA|nvlddmkm|amdkmdag|igfx|Display"
$recentAppErrors = Get-RecentLogEvents -LogName "Application" -ProviderPattern "RainbowSix|Application Error|Application Hang"

# ── Byg payload ──────────────────────────────────────────────────────────
$payload = [PSCustomObject]@{
    spiller = $PlayerName
    netvaerk = [PSCustomObject]@{
        forbindelsestype = $connectionType
        ping_ms = $ping.AvgLatencyMs
        pakketab_pct = $ping.PacketLossPct
        vpn_aktiv = $vpnActive
    }
    lyd_discord = & {
        # headset_software only gets included at all when non-empty - the AI
        # kept inventing a "not configured" warning when it saw an empty
        # array, even when explicitly told not to, so the reliable fix is to
        # just not show it the field rather than fight prompt compliance.
        $lydDiscord = [ordered]@{
            lydenheder = $audioDevices
            discord_koerer = $discordRunning
            discord_version = $discordVersion
        }
        if ($runningHeadsetSoftware -and @($runningHeadsetSoftware).Count -gt 0) {
            $lydDiscord.headset_software = @($runningHeadsetSoftware)
        }
        [PSCustomObject]$lydDiscord
    }
    spil_launcher = [PSCustomObject]@{
        r6_fundet = $null -ne $r6Path
        r6_sidst_aendret = $r6LastModified
        r6_dage_siden_opdateret = $r6DageSidenOpdateret
        ubisoft_connect_koerer = $ubisoftConnectRunning
        battleye_status = $battlEyeStatus
    }
    system = [PSCustomObject]@{
        gpu_navn = $gpu.Name
        gpu_driver_dato = $gpu.DriverDate
        genstart_afventer = $pendingReboot
        dage_siden_genstart = $dageSidenGenstart
        stroemplan = $powerPlan
        ledig_diskplads_gb = $freeDiskGB
        top_processer = $topProcesses
        pc_model = $pcModel
        mus_navn = $mouseName
        tastatur_navn = $keyboardName
        brandet_udstyr = $brandedPeripherals
        cpu_navn = $cpuName
        ram_gb = $ramGB
        os_version = $osVersion
        skaerm = $screenInfo
        nylige_system_fejl = $recentSystemErrors
        nylige_app_fejl = $recentAppErrors
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

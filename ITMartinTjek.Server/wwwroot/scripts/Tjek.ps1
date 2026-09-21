# Tjek.ps1 - dybt tjek af en Windows-pc for tjek.itmartin.dk
# Højreklik -> "Kør med PowerShell". Ingen administrator nødvendig (nogle punkter
# viser mindre uden). Sender KUN de viste tekniske punkter til tjek.itmartin.dk under
# det enhedsnavn du skriver - ingen filer, ingen adgangskoder, ingen personlige data.

& "$env:SystemRoot\System32\chcp.com" 65001 > $null
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$Endpoint = "https://tjek.itmartin.dk/api/deep"

function Try-Get { param([scriptblock]$Block, $Default = $null) try { & $Block } catch { $Default } }
$checks = New-Object System.Collections.Generic.List[object]
function Add-Check { param($Id, $Name, $Status, $Value, $Hint = "") $checks.Add([pscustomobject]@{ id = $Id; name = $Name; status = $Status; value = "$Value"; hint = $Hint }) }

$NameFile = "$env:LOCALAPPDATA\ITMartinTjek\device.txt"
$Device = Try-Get { (Get-Content -Path $NameFile -ErrorAction Stop).Trim() }
if ([string]::IsNullOrWhiteSpace($Device)) {
    $Device = Read-Host "Navn på denne enhed (samme som på tjek-siden)"
    Try-Get { New-Item -ItemType Directory -Path (Split-Path $NameFile) -Force | Out-Null; Set-Content -Path $NameFile -Value $Device }
}

Write-Host "Kigger på $env:COMPUTERNAME ..." -ForegroundColor Cyan

# ── System ────────────────────────────────────────────────────────────────
$os = Try-Get { Get-CimInstance Win32_OperatingSystem }
if ($os) {
    $up = (Get-Date) - $os.LastBootUpTime
    Add-Check "os" "Windows" "info" ("{0} build {1}" -f $os.Caption, $os.BuildNumber)
    Add-Check "uptime" "Tid siden genstart" ($(if ($up.TotalDays -gt 7) { "warn" } else { "ok" })) ("{0:0} dage {1} timer" -f $up.TotalDays, $up.Hours) $(if ($up.TotalDays -gt 7) { "Over en uge uden genstart - mange små fejl forsvinder ved en genstart." })
    $freeGb = [math]::Round($os.FreePhysicalMemory / 1MB, 1); $totGb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    Add-Check "ram" "Hukommelse" ($(if ($freeGb -lt 1) { "warn" } else { "ok" })) ("{0} GB ledig af {1} GB" -f $freeGb, $totGb) $(if ($freeGb -lt 1) { "Næsten al hukommelse er i brug - luk programmer eller genstart." })
}
$cs = Try-Get { Get-CimInstance Win32_ComputerSystem }
if ($cs) { Add-Check "model" "Model" "info" ("{0} {1}" -f $cs.Manufacturer, $cs.Model) }
$cpu = Try-Get { Get-CimInstance Win32_Processor | Select-Object -First 1 }
if ($cpu) { Add-Check "cpu" "Processor" "info" ("{0} ({1}% belastet nu)" -f $cpu.Name.Trim(), $cpu.LoadPercentage) }

# Disks
Try-Get {
    foreach ($d in Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3") {
        $free = [math]::Round($d.FreeSpace / 1GB); $size = [math]::Round($d.Size / 1GB); $pct = if ($size) { [math]::Round($free / $size * 100) } else { 0 }
        Add-Check "disk-$($d.DeviceID)" "Disk $($d.DeviceID)" ($(if ($pct -lt 5) { "bad" } elseif ($pct -lt 15) { "warn" } else { "ok" })) ("{0} GB ledig af {1} GB ({2}%)" -f $free, $size, $pct) $(if ($pct -lt 15) { "Lidt ledig plads - Windows bliver langsomt og opdateringer fejler under ~10 %." })
    }
}
Try-Get {
    foreach ($p in Get-PhysicalDisk) {
        if ($p.HealthStatus -ne "Healthy") { Add-Check "pd-$($p.DeviceId)" "Fysisk disk $($p.FriendlyName)" "bad" $p.HealthStatus "Disken melder fejl - tag backup NU." }
        else { Add-Check "pd-$($p.DeviceId)" "Fysisk disk $($p.FriendlyName)" "ok" ("{0}, {1}" -f $p.MediaType, $p.HealthStatus) }
    }
}

# Pending reboot
$pending = Try-Get { (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") -or (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") } $false
Add-Check "reboot" "Venter på genstart" ($(if ($pending) { "warn" } else { "ok" })) $(if ($pending) { "Ja" } else { "Nej" }) $(if ($pending) { "En opdatering er kun halvt installeret - genstart før du fejlsøger andet." })

# ── Enheder med fejl ─────────────────────────────────────────────────────
$bad = Try-Get { Get-PnpDevice -PresentOnly -Status Error, Degraded, Unknown -ErrorAction Stop | Where-Object { $_.Status -ne "OK" } } @()
if ($bad.Count -eq 0) { Add-Check "pnp" "Enheder med fejl" "ok" "Ingen" }
foreach ($d in $bad) {
    $code = Try-Get { (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName DEVPKEY_Device_ProblemCode -ErrorAction Stop).Data }
    $hint = switch ($code) { 10 { "Kode 10: enheden kunne ikke starte - tag stikket ud og i igen, prøv en anden USB-port, ellers driver." } 28 { "Kode 28: driver mangler - hent den fra producenten." } 43 { "Kode 43: USB-enheden svarede forkert - andet kabel/port." } 45 { "Kode 45: ikke tilsluttet (spøgelse) - kan fjernes." } default { "Se problemkoden i Enhedshåndtering (Device Manager)." } }
    Add-Check "pnp-$($d.InstanceId)" "Enhed med fejl: $($d.FriendlyName)" ($(if ($code -eq 45) { "warn" } else { "bad" })) ("{0}, kode {1}, {2}" -f $d.Status, $code, $d.Class) $hint
}
# Ghost devices (not present) in audio/USB classes - cleanup candidates
$ghosts = Try-Get { Get-PnpDevice -Class AudioEndpoint, MEDIA, USB, Camera, Image -ErrorAction Stop | Where-Object { -not $_.Present } } @()
if ($ghosts.Count -gt 0) { Add-Check "ghosts" "Frakoblede/gamle enheder (lyd, USB, kamera)" ($(if ($ghosts.Count -gt 15) { "warn" } else { "info" })) ("{0}: {1}" -f $ghosts.Count, (($ghosts | Select-Object -First 8 | ForEach-Object { $_.FriendlyName }) -join ", ")) "Rester efter enheder der ikke er sat til. Ufarlige, men kan forvirre lydvalg. Kan fjernes i Enhedshåndtering med 'Vis skjulte enheder'." }
# Device restarts since boot (Kernel-PnP 219 = failed to load driver on a device)
$pnpEvents = Try-Get { (Get-WinEvent -FilterHashtable @{ LogName = "System"; ProviderName = "Microsoft-Windows-Kernel-PnP"; Id = 219; StartTime = $os.LastBootUpTime } -ErrorAction Stop).Count } 0
Add-Check "pnp219" "Driver-fejl ved enhedsstart siden genstart" ($(if ($pnpEvents -gt 3) { "warn" } elseif ($pnpEvents -gt 0) { "info" } else { "ok" })) $pnpEvents $(if ($pnpEvents -gt 3) { "En enhed fejler gentagne gange ved start - typisk et USB-kabel/hub eller en driver." })

# ── Lyd ───────────────────────────────────────────────────────────────────
$audio = Try-Get { Get-PnpDevice -Class AudioEndpoint -PresentOnly -ErrorAction Stop } @()
$audioOk = @($audio | Where-Object Status -eq "OK")
Add-Check "audio" "Lydenheder (aktive)" ($(if ($audioOk.Count -eq 0) { "bad" } else { "ok" })) ("{0}: {1}" -f $audioOk.Count, (($audioOk | ForEach-Object { $_.FriendlyName }) -join ", ")) $(if ($audioOk.Count -eq 0) { "Windows har ingen aktive lydenheder - USB-lydenheden kan være i fejl (se ovenfor)." })
$audioSvc = Try-Get { (Get-Service Audiosrv).Status }
Add-Check "audiosvc" "Windows Audio-tjeneste" ($(if ($audioSvc -eq "Running") { "ok" } else { "bad" })) $audioSvc $(if ($audioSvc -ne "Running") { "Lydtjenesten kører ikke - genstart pc'en." })
$audioApps = Try-Get { Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match "Nahimic|Sonic Studio|Realtek Audio Console|DTS|Dolby|Voicemod|SteelSeries GG|Logitech G HUB|Razer Synapse|HyperX NGENUITY|Waves MaxxAudio" } | ForEach-Object { $_.DisplayName } | Sort-Object -Unique } @()
if ($audioApps.Count -gt 0) { Add-Check "audioapps" "Lydprogrammer der kan blande sig" "info" ($audioApps -join ", ") "Lydforbedringsprogrammer kan overtage mikrofon/udgang. Slå dem fra først, hvis lyden driller." }

# ── Netværk ──────────────────────────────────────────────────────────────
$adapter = Try-Get { Get-NetAdapter | Where-Object Status -eq "Up" | Select-Object -First 1 }
if ($adapter) {
    $wifi = $adapter.MediaType -match "802.11" -or $adapter.InterfaceDescription -match "Wireless|Wi-Fi"
    Add-Check "net" "Netværk" "info" ("{0} - {1} ({2})" -f $(if ($wifi) { "Wi-Fi" } else { "Kabel" }), $adapter.InterfaceDescription, $adapter.LinkSpeed)
}
$ping = Try-Get { $r = Test-Connection 1.1.1.1 -Count 4 -ErrorAction Stop; [pscustomobject]@{ Avg = [math]::Round(($r | Measure-Object ResponseTime -Average).Average); Loss = [math]::Round((4 - $r.Count) / 4 * 100) } }
if ($ping) { Add-Check "ping" "Ping til internettet" ($(if ($ping.Loss -gt 0 -or $ping.Avg -gt 100) { "warn" } else { "ok" })) ("{0} ms, {1}% tab" -f $ping.Avg, $ping.Loss) $(if ($ping.Loss -gt 0) { "Pakketab - dårligt Wi-Fi eller kabel." }) } else { Add-Check "ping" "Ping til internettet" "bad" "Intet svar" "Ingen forbindelse til internettet." }
$dns = Try-Get { (Resolve-DnsName itmartin.dk -ErrorAction Stop | Select-Object -First 1).IPAddress }
Add-Check "dns" "DNS-opslag" ($(if ($dns) { "ok" } else { "bad" })) $(if ($dns) { "Virker" } else { "Fejler" }) $(if (-not $dns) { "Navneopslag virker ikke - genstart routeren, eller sæt DNS til 1.1.1.1." })

# ── Drivere / opdateringer ───────────────────────────────────────────────
$gpu = Try-Get { Get-CimInstance Win32_VideoController | Select-Object -First 1 }
if ($gpu) { $age = (Get-Date) - $gpu.DriverDate; Add-Check "gpu" "Skærmkort-driver" ($(if ($age.TotalDays -gt 365) { "warn" } else { "ok" })) ("{0}, driver {1} ({2:d})" -f $gpu.Name, $gpu.DriverVersion, $gpu.DriverDate) $(if ($age.TotalDays -gt 365) { "Over et år gammel driver." }) }
$oldDrivers = Try-Get { Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DeviceClass -in "MEDIA", "NET", "USB" -and $_.DriverDate -and $_.DriverDate -lt (Get-Date).AddYears(-5) -and $_.Manufacturer -ne "Microsoft" } | Select-Object -First 6 | ForEach-Object { "{0} ({1:yyyy})" -f $_.DeviceName, $_.DriverDate } } @()
if ($oldDrivers.Count -gt 0) { Add-Check "olddrv" "Gamle drivere (lyd/net/USB, >5 år)" "info" ($oldDrivers -join ", ") "Ikke nødvendigvis et problem, men det første sted at kigge hvis netop den enhed driller." }
$lastUpdate = Try-Get { (Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 1).InstalledOn }
if ($lastUpdate) { $ud = (Get-Date) - $lastUpdate; Add-Check "update" "Seneste Windows-opdatering" ($(if ($ud.TotalDays -gt 60) { "warn" } else { "ok" })) ("{0:d}" -f $lastUpdate) $(if ($ud.TotalDays -gt 60) { "Over to måneder siden - kør Windows Update." }) }

# ── Send ─────────────────────────────────────────────────────────────────
$payload = [pscustomobject]@{ device = $Device; computer = $env:COMPUTERNAME; at = (Get-Date).ToString("o"); checks = $checks }
$json = $payload | ConvertTo-Json -Depth 5
Write-Host ""
foreach ($c in $checks) { $color = switch ($c.status) { "ok" { "Green" } "warn" { "Yellow" } "bad" { "Red" } default { "Gray" } }; Write-Host ("[{0,-4}] {1}: {2}" -f $c.status, $c.name, $c.value) -ForegroundColor $color }
Write-Host ""
try {
    $r = Invoke-RestMethod -Uri $Endpoint -Method Post -Body ([System.Text.Encoding]::UTF8.GetBytes($json)) -ContentType "application/json; charset=utf-8" -TimeoutSec 30
    Write-Host $r.message -ForegroundColor Green
} catch {
    Write-Host "Kunne ikke sende til $Endpoint - $($_.Exception.Message)" -ForegroundColor Red
    $json | Set-Content -Path "$env:TEMP\tjek.json" -Encoding UTF8
    Write-Host "Resultatet er gemt i $env:TEMP\tjek.json"
}
Write-Host ""
Read-Host "Tryk Enter for at lukke"

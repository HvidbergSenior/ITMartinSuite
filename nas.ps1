# NAS container control
#
# Usage:
#   .\nas.ps1 up auction-web       # start a single container
#   .\nas.ps1 down auction-web     # stop it
#   .\nas.ps1 up filesorter        # start the filesorter group
#   .\nas.ps1 status               # see what is running
#
# Or dot-source and use functions directly:
#   . .\nas.ps1
#   nas-up auction-web
#   nas-up filesorter
#   nas-status

$NasUser      = "martinhvidberg"
$NasLocal     = "10.0.0.126"
$NasTailscale = "100.117.120.44"
$DIR          = "/volume1/homes/MartinHvidberg/martinsuite-magic"

$NasIp = $null
foreach ($ip in @($NasLocal, $NasTailscale)) {
    $ok = (Test-NetConnection -ComputerName $ip -Port 22 -InformationLevel Quiet -WarningAction SilentlyContinue -ErrorAction SilentlyContinue)
    if ($ok) { $NasIp = $ip; break }
}
if (-not $NasIp) { Write-Error "NAS not reachable on $NasLocal or $NasTailscale"; exit 1 }
Write-Host "NAS reachable at $NasIp" -ForegroundColor DarkGray
$NAS = "$NasUser@$NasIp"

# Named groups — shortcuts for multi-container sets
$Groups = @{
    "filesorter" = @("rabbitmq", "filesorter-web", "filesorter-worker")
    "magazine"   = @("magazine-web", "magazine-search-web")
    "claude"     = @("library-web", "library-search-web", "curator-web", "magic-web", "filesorter-web", "receipt-web", "magazine-web", "budget-web")
}

# ── Always-on (no profile, started by docker compose up -d) ──────────────
#   receipt-web, library-web, adhd-web
#   magic-web, magic-postgres, magic-collection-web
#   gallery-web, gallery-mie, gallery-hvidbergfamily, gallery-hvidberg
#   testhub-web, club-web
#   index-web, cloudflared

# ── Manual (profiles: [manual]) — use nas-up / nas-down ──────────────────
#   musik-web          — public music sharing
#   family-web         — family planner
#   musik-studio-web   — private studio
#   budget-web         — personal budget
#   r6assistant-web    — gaming tool
#   library-search-web — book search
#   curator-web        — media curator
#   magazine-web       — magazine scanner
#   magazine-search-web
#   filesorter-web     — file sorter (use group: filesorter)
#   filesorter-worker  — file sorter worker
#   rabbitmq           — message broker (auto-started with filesorter)
#   image-processor    — image worker
#   imagegen-web       — AI image generator (port 8107)
#   auction-web        — auction (testing soon)
#   bartab-web         — bar tab (not in use yet)
#   market-web         — marketplace (not in use yet)

function nas-up {
    param([Parameter(Mandatory)][string]$Name)
    if ($Groups.ContainsKey($Name)) {
        $services = $Groups[$Name] -join " "
        Write-Host "Starting group '$Name': $services ..." -ForegroundColor Cyan
        ssh $NAS "cd $DIR && docker compose up -d $services"
    } else {
        Write-Host "Starting $Name ..." -ForegroundColor Cyan
        ssh $NAS "cd $DIR && docker compose up -d $Name"
    }
}

function nas-down {
    param([Parameter(Mandatory)][string]$Name)
    if ($Groups.ContainsKey($Name)) {
        $services = $Groups[$Name] -join " "
        Write-Host "Stopping group '$Name': $services ..." -ForegroundColor Yellow
        ssh $NAS "cd $DIR && docker compose stop $services"
    } else {
        Write-Host "Stopping $Name ..." -ForegroundColor Yellow
        ssh $NAS "cd $DIR && docker compose stop $Name"
    }
}

function nas-status {
    Write-Host "Running containers on NAS:" -ForegroundColor Cyan
    ssh $NAS "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"
}

function nas-push-env {
    Write-Host "Pushing magic.env to NAS and restarting all Claude services..." -ForegroundColor Cyan
    scp magic.env "${NAS}:${DIR}/magic.env"
    if ($LASTEXITCODE -ne 0) { Write-Error "SCP failed"; return }
    $services = $Groups["claude"] -join " "
    ssh $NAS "cd $DIR && docker compose up -d --force-recreate $services"
    Write-Host "Done." -ForegroundColor Green
}

function nas-deploy {
    param([Parameter(Mandatory)][string]$Service)
    Write-Host "Deploying $Service (pull + build + up) ..." -ForegroundColor Green
    ssh $NAS "cd $DIR && git pull && docker compose up -d --build $Service"
}

# Run as script: .\nas.ps1 up auction-web
if ($args.Count -ge 1) {
    switch ($args[0]) {
        "up"     { nas-up     $args[1] }
        "down"   { nas-down   $args[1] }
        "status" { nas-status }
        "deploy"   { nas-deploy   $args[1] }
        "push-env" { nas-push-env }
        default    { Write-Host "Usage: .\nas.ps1 up|down|status|deploy|push-env [name]" }
    }
}

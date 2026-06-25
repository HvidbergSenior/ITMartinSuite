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

$NAS = "martinhvidberg@100.117.120.44"
$DIR = "/volume1/homes/MartinHvidberg/martinsuite-magic"

# Named groups — shortcuts for multi-container sets
$Groups = @{
    "filesorter" = @("rabbitmq", "filesorter-web", "filesorter-worker")
    "magazine"   = @("magazine-web", "magazine-search-web")
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
        "deploy" { nas-deploy $args[1] }
        default  { Write-Host "Usage: .\nas.ps1 up|down|status|deploy [name]" }
    }
}

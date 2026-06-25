# NAS container control
#
# Usage:
#   .\nas.ps1 up auction-web       # start a container
#   .\nas.ps1 down auction-web     # stop it
#   .\nas.ps1 status               # see what is running
#
# Or load into your shell and use the functions directly:
#   . .\nas.ps1
#   nas-up auction-web
#   nas-down musik-web
#   nas-status

$NAS = "martinhvidberg@100.117.120.44"
$DIR = "/volume1/homes/MartinHvidberg/martinsuite-magic"

# On-demand services (profiles: [manual] in docker-compose — not started by default)
# Start/stop freely without affecting other containers.
#
#   musik-web    — public music sharing, open when people should listen
#   family-web   — family planner, open when needed
#   bartab-web   — bar tab (not in use yet)
#   auction-web  — auction (testing soon)
#   market-web   — marketplace (not in use yet)

function nas-up {
    param([Parameter(Mandatory)][string]$Service)
    Write-Host "Starting $Service ..." -ForegroundColor Cyan
    ssh $NAS "cd $DIR && docker compose up -d $Service"
}

function nas-down {
    param([Parameter(Mandatory)][string]$Service)
    Write-Host "Stopping $Service ..." -ForegroundColor Yellow
    ssh $NAS "cd $DIR && docker compose stop $Service"
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
        default  { Write-Host "Usage: .\nas.ps1 up|down|status|deploy [service-name]" }
    }
}

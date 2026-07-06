param(
    [string]$NasIp = "10.0.0.126",
    [string]$NasUser = "martinhvidberg",
    [string]$ComposePath = "~/martinsuite-magic"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ComposeFile = Join-Path $ScriptDir "docker-compose.yaml"

Write-Host "=== NAS Recovery ===" -ForegroundColor Cyan

# 1. Copy the updated compose file
Write-Host "`n[1/3] Copying docker-compose.yaml to NAS..." -ForegroundColor Yellow
Get-Content $ComposeFile -Raw | ssh "${NasUser}@${NasIp}" "cat > ${ComposePath}/docker-compose.yaml"
if ($LASTEXITCODE -ne 0) { Write-Host "Copy failed - is the NAS up?" -ForegroundColor Red; exit 1 }

# 2. Stop all non-essential containers
Write-Host "`n[2/3] Stopping non-essential containers..." -ForegroundColor Yellow
$stopList = "filesorter-web filesorter-worker curator-web budget-web bartab-web auction-web market-web imagegen-web cloudoverblik-web testhub-web magazine-web magazine-search-web musik-studio-web r6assistant-web r6intel-web r6strat-web live-web image-processor scan-web upload-web dailybrief-web poll-web mediaseller-web musik-web magic-collection-web library-search-web"

ssh "${NasUser}@${NasIp}" "cd ${ComposePath}; docker compose stop $stopList"

# 3. Start always-on services with new policies
Write-Host "`n[3/3] Starting essential containers..." -ForegroundColor Yellow
$startList = "cloudflared magic-postgres rabbitmq index-web magic-web receipt-web library-web adhd-web family-web gallery-web club-web stats-web"

ssh "${NasUser}@${NasIp}" "cd ${ComposePath}; docker compose up -d $startList"

Write-Host "`nDone! Containers now running:" -ForegroundColor Green
ssh "${NasUser}@${NasIp}" "docker ps --format '{{.Names}} | {{.Status}}'"

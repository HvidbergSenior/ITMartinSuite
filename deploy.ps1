param(
    [Parameter(Mandatory)]
    [string]$Service,
    [switch]$Remote
)

$ProgressPreference = 'SilentlyContinue'

$NasUser      = "martinhvidberg"
$NasLocal     = "10.0.0.126"
$NasTailscale = "100.117.120.44"
$NasPath      = "~/martinsuite-magic"
$NasFile_Base = "/volume1/homes/MartinHvidberg/martinsuite-magic"

if ($Remote) {
    $NasIp = $NasTailscale
    Write-Host "Remote mode - using Tailscale ($NasTailscale)" -ForegroundColor DarkGray
} else {
    $ok = (Test-NetConnection -ComputerName $NasLocal -Port 22 -InformationLevel Quiet -WarningAction SilentlyContinue -ErrorAction SilentlyContinue)
    if (-not $ok) { Write-Error "NAS not reachable on local network ($NasLocal). Use -Remote flag if outside."; exit 1 }
    $NasIp = $NasLocal
    Write-Host "NAS reachable at $NasIp" -ForegroundColor DarkGray
}
$NasHost = "$NasUser@$NasIp"

# Reconnect persistent mapped drives (Z: may have dropped after reboot)
net use * /persistent:yes 2>$null | Out-Null

$ServiceMap = @{
    "curator-web"            = @{ Dockerfile = "ITMartin.Curator.Server/Dockerfile";                  Context = "." }
    "magic-web"              = @{ Dockerfile = "ITMartin.Magic.Server/Dockerfile";                    Context = "." }
    "magic-collection-web"  = @{ Dockerfile = "ITMartin.MagicCollection.Server/Dockerfile";           Context = "." }
    "filesorter-web"         = @{ Dockerfile = "ITMartinFileSorter.Server/Dockerfile";                Context = "." }
    "filesorter-worker"      = @{ Dockerfile = "ITMartinFileSorter.Worker/Dockerfile";                Context = "." }
    "gallery-web"            = @{ Dockerfile = "ITMartinFileSorter.Gallery.Server/Dockerfile";        Context = "." }
    "gallery-mie"            = @{ Dockerfile = "ITMartinFileSorter.Gallery.Server/Dockerfile";        Context = "." }
    "gallery-hvidbergfamily" = @{ Dockerfile = "ITMartinFileSorter.Gallery.Server/Dockerfile";        Context = "." }
    "gallery-hvidberg"       = @{ Dockerfile = "ITMartinFileSorter.Gallery.Server/Dockerfile";        Context = "." }
    "budget-web"             = @{ Dockerfile = "ITMartinBudget.Server/Dockerfile";                    Context = "." }
    "r6assistant-web"        = @{ Dockerfile = "ITMartinR6Assistant.Server/Dockerfile";               Context = "." }
    "r6intel-web"            = @{ Dockerfile = "ITMartinR6Intel.Server/Dockerfile";                   Context = "." }
    "receipt-web"            = @{ Dockerfile = "ITMartin.Receipt.Server/Dockerfile";                  Context = "." }
    "library-web"            = @{ Dockerfile = "ITMartinLibrary.Server/Dockerfile";                   Context = "." }
    "library-search-web"     = @{ Dockerfile = "ITMartinLibrary.Search.Server/Dockerfile";            Context = "." }
    "adhd-web"               = @{ Dockerfile = "ITMartinAdhd.Server/Dockerfile";                      Context = "." }
    "family-web"             = @{ Dockerfile = "ITMartinFamily.Server/Dockerfile";                    Context = "." }
    "market-web"             = @{ Dockerfile = "ITMartinMarket.Server/Dockerfile";                    Context = "." }
    "bartab-web"             = @{ Dockerfile = "ITMartinBarTab.Server/Dockerfile";                    Context = "." }
    "auction-web"            = @{ Dockerfile = "ITMartinAuction.Server/Dockerfile";                   Context = "."; Profile = "manual" }
    "testhub-web"            = @{ Dockerfile = "ITMartinTestHub.Server/Dockerfile";                   Context = "." }
    "index-web"              = @{ Dockerfile = "ITMartin.IndexServer/Dockerfile";                     Context = "." }
    "musik-web"              = @{ Dockerfile = "ITMartinMusic.Server/Dockerfile";                     Context = "." }
    "club-web"               = @{ Dockerfile = "ITMartinClub.Server/Dockerfile";                      Context = "." }
    "magazine-web"           = @{ Dockerfile = "ITMartinMagazine.Server/Dockerfile";                  Context = "." }
    "magazine-search-web"    = @{ Dockerfile = "ITMartinMagazine.Search.Server/Dockerfile";           Context = "." }
    "musik-studio-web"       = @{ Dockerfile = "ITMartinMusikStudio.Server/Dockerfile";               Context = "." }
    "image-processor"        = @{ Dockerfile = "ITMartinImageProcessor.Worker/Dockerfile";            Context = "ITMartinImageProcessor.Worker" }
    "scan-web"               = @{ Dockerfile = "ITMartinScan.Server/Dockerfile";                      Context = "." }
    "imagegen-web"           = @{ Dockerfile = "ITMartinImageGen.Server/Dockerfile";                  Context = "." }
    "dailybrief-web"         = @{ Dockerfile = "ITMartinDailyBrief.Server/Dockerfile";                Context = "." }
    "poll-web"               = @{ Dockerfile = "ITMartinPoll.Server/Dockerfile";                       Context = "." }
    "r6strat-web"            = @{ Dockerfile = "ITMartinR6Strat.Server/Dockerfile";                    Context = "."; Profile = "manual" }
    "live-web"               = @{ Dockerfile = "ITMartinLive.Server/Dockerfile";                       Context = "."; Profile = "manual" }
    "upload-web"             = @{ Dockerfile = "ITMartinUpload.Server/Dockerfile";                     Context = "."; Profile = "manual" }
    "cloudoverblik-web"      = @{ Dockerfile = "ITMartinCloudOverblik.Server/Dockerfile";              Context = "."; Profile = "manual" }
}

if (-not $ServiceMap.ContainsKey($Service)) {
    $validNames = $ServiceMap.Keys -join ", "
    Write-Error "Unknown service: $Service. Valid: $validNames"
    exit 1
}

$entry      = $ServiceMap[$Service]
$imageName  = "martinsuite-$Service"
$dockerfile = $entry.Dockerfile
$context    = $entry.Context
$tarName    = "$imageName.tar"
$nasFile    = "$NasFile_Base/$tarName"

Write-Host "[1/3] Building $imageName..." -ForegroundColor Cyan
docker build --platform linux/amd64 --provenance=false -t $imageName -f $dockerfile $context
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[2/3] Saving image..." -ForegroundColor Cyan

if (Test-Path "Z:\martinsuite-magic") {
    $dest = "Z:\martinsuite-magic\$tarName"
    Write-Host "    Copying directly to NAS via Z: ..." -ForegroundColor Yellow
    docker save -o $dest $imageName
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Write-Host "    Done." -ForegroundColor Green
} else {
    $dest = "$env:TEMP\$tarName"
    Write-Host "    Z: not available, saving locally then SCP to NAS..." -ForegroundColor Yellow
    docker save -o $dest $imageName
    if ($LASTEXITCODE -ne 0) { exit 1 }
    scp -O $dest "${NasHost}:${nasFile}"
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Remove-Item $dest -Force
}

Write-Host "[3/3] Loading on NAS and restarting $Service..." -ForegroundColor Cyan

$composeFile = Join-Path $PSScriptRoot "docker-compose.yaml"
scp -O $composeFile "${NasHost}:${NasPath}/docker-compose.yaml" | Out-Null

$profileFlag = if ($entry.Profile) { "--profile $($entry.Profile) " } else { "" }
$sshCmd = "docker --context default load -i " + $nasFile + " && rm " + $nasFile + " && cd " + $NasPath + " && docker --context default compose " + $profileFlag + "up -d --force-recreate --timeout 10 " + $Service
ssh $NasHost "$sshCmd"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[Cleanup] Removing local image and pruning Docker..." -ForegroundColor DarkGray
docker rmi $imageName 2>$null | Out-Null
docker image prune -f | Out-Null
docker builder prune -f | Out-Null

Write-Host "Done! $Service is deployed." -ForegroundColor Green

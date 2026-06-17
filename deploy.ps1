param(
    [Parameter(Mandatory)]
    [string]$Service
)

$NasHost = "martinhvidberg@100.117.120.44"
$NasPath = "~/martinsuite-magic"

$ServiceMap = @{
    "magic-web"         = @{ Dockerfile = "ITMartin.Magic.Server/Dockerfile";       Context = "." }
    "filesorter-web"    = @{ Dockerfile = "ITMartinFileSorter.Server/Dockerfile";    Context = "." }
    "filesorter-worker" = @{ Dockerfile = "ITMartinFileSorter.Worker/Dockerfile";    Context = "." }
    "budget-web"        = @{ Dockerfile = "ITMartinBudget.Server/Dockerfile";        Context = "." }
    "r6assistant-web"   = @{ Dockerfile = "ITMartinR6Assistant.Server/Dockerfile";   Context = "." }
    "receipt-web"       = @{ Dockerfile = "ITMartin.Receipt.Server/Dockerfile";      Context = "." }
    "library-web"       = @{ Dockerfile = "ITMartinLibrary.Server/Dockerfile";       Context = "." }
    "adhd-web"          = @{ Dockerfile = "ITMartinAdhd.Server/Dockerfile";          Context = "." }
    "index-web"         = @{ Dockerfile = "ITMartin.IndexServer/Dockerfile";         Context = "." }
    "image-processor"   = @{ Dockerfile = "ITMartinImageProcessor.Worker/Dockerfile"; Context = "ITMartinImageProcessor.Worker" }
}

if (-not $ServiceMap.ContainsKey($Service)) {
    Write-Error "Unknown service '$Service'. Valid: $($ServiceMap.Keys -join ', ')"
    exit 1
}

$entry      = $ServiceMap[$Service]
$imageName  = "martinsuite-$Service"
$dockerfile = $entry.Dockerfile
$context    = $entry.Context

Write-Host "[1/3] Building $imageName..." -ForegroundColor Cyan
docker build -t $imageName -f $dockerfile $context
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[2/3] Transferring to NAS..." -ForegroundColor Cyan
docker save $imageName | ssh $NasHost "docker load"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[3/3] Restarting $Service on NAS..." -ForegroundColor Cyan
ssh $NasHost "cd $NasPath && docker compose up -d $Service"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Done! $Service is deployed." -ForegroundColor Green

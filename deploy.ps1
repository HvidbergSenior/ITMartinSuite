param(
    [Parameter(Mandatory)]
    [string]$Service
)

$NasHost = "martinhvidberg@100.117.120.44"
$NasPath = "~/martinsuite-magic"
$SyncFolder = "C:\Users\hvidb\SynologyDrive\martinsuite-magic"

$ServiceMap = @{
    "magic-web"         = @{ Dockerfile = "ITMartin.Magic.Server/Dockerfile";        Context = "." }
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
$syncFile   = "$SyncFolder\$imageName.tar"

Write-Host "[1/3] Building $imageName..." -ForegroundColor Cyan
docker build --platform linux/amd64 --provenance=false -t $imageName -f $dockerfile $context
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[2/3] Saving to Synology Drive..." -ForegroundColor Cyan
docker save -o $syncFile $imageName
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "    Waiting for sync (30s)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host "[3/3] Loading on NAS and restarting $Service..." -ForegroundColor Cyan
$nasFile = "/volume1/homes/MartinHvidberg/martinsuite-magic/$imageName.tar"
ssh $NasHost "docker --context default load -i $nasFile && rm $nasFile && cd $NasPath && docker --context default compose up -d $Service"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Done! $Service is deployed." -ForegroundColor Green

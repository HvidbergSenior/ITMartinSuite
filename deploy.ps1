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
$nasFile    = "/volume1/homes/MartinHvidberg/martinsuite-magic/$imageName.tar"

Write-Host "[1/3] Building $imageName..." -ForegroundColor Cyan
docker build --platform linux/amd64 --provenance=false -t $imageName -f $dockerfile $context
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[2/3] Saving to Synology Drive..." -ForegroundColor Cyan
docker save -o $syncFile $imageName
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "    Waiting for file to appear on NAS..." -ForegroundColor Yellow
$timeout = 300
$elapsed = 0
$found = $false
while ($elapsed -lt $timeout) {
    Start-Sleep -Seconds 5
    $elapsed += 5
    $check = ssh $NasHost "test -f '$nasFile' && echo yes || echo no"
    Write-Host "    ${elapsed}s - $check" -ForegroundColor DarkGray
    if ($check.Trim() -eq "yes") {
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Error "File did not appear on NAS after ${timeout}s. Check Synology Drive sync."
    exit 1
}

Write-Host "[3/3] Loading on NAS and restarting $Service..." -ForegroundColor Cyan
ssh $NasHost "docker --context default load -i '$nasFile' && rm '$nasFile' && cd $NasPath && docker --context default compose up -d $Service"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Done! $Service is deployed." -ForegroundColor Green

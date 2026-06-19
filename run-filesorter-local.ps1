param(
    [string]$SourcePath = "",
    [string]$LibraryPath = ""
)

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Read-Host "Source folder (where files to sort are)"
}
if ([string]::IsNullOrWhiteSpace($LibraryPath)) {
    $LibraryPath = Read-Host "Library folder (where sorted files go)"
}

$SourcePath  = $SourcePath.TrimEnd('\')
$LibraryPath = $LibraryPath.TrimEnd('\')
$DbPath      = Join-Path $LibraryPath ".media.db"

Write-Host ""
Write-Host "Source  : $SourcePath"
Write-Host "Library : $LibraryPath"
Write-Host ""

# Ensure RabbitMQ is running locally
$rabbit = docker ps --filter "name=rabbitmq-local" --format "{{.Names}}" 2>$null
if ($rabbit -ne "rabbitmq-local") {
    Write-Host "Starting RabbitMQ..."
    docker run -d --name rabbitmq-local --rm -p 5672:5672 -p 15672:15672 rabbitmq:3-management | Out-Null
    Start-Sleep -Seconds 5
}
else {
    Write-Host "RabbitMQ already running."
}

$env:MediaSettings__SourceRoot  = $SourcePath
$env:MediaSettings__LibraryRoot = $LibraryPath
$env:ConnectionStrings__MediaDb = "Data Source=$DbPath"
$env:RabbitMq__Host             = "localhost"

Write-Host "Starting FileSorter Worker..."
$worker = Start-Process -PassThru -NoNewWindow dotnet -ArgumentList "run --project ITMartinFileSorter.Worker/ITMartinFileSorter.Worker.csproj"

Write-Host "Starting FileSorter Web on http://localhost:8080 ..."
$web = Start-Process -PassThru -NoNewWindow dotnet -ArgumentList "run --project ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj --urls http://localhost:8080"

Write-Host ""
Write-Host "FileSorter running at http://localhost:8080"
Write-Host "Press Ctrl+C to stop."

try {
    Wait-Process -Id $web.Id
}
finally {
    Stop-Process -Id $worker.Id -ErrorAction SilentlyContinue
    Stop-Process -Id $web.Id   -ErrorAction SilentlyContinue
    Write-Host "Stopped."
}

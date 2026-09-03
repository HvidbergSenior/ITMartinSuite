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
}
else {
    Write-Host "RabbitMQ already running."
}

# Wait for the AMQP port to actually accept connections before starting the
# Worker. A fixed sleep isn't reliable - RabbitMqBackgroundJobQueue's
# constructor calls factory.CreateConnection() with no retry/AutomaticRecovery,
# so if the broker isn't ready yet the Worker throws an unhandled
# BrokerUnreachableException and the whole host dies (this is exactly what
# killed filesorter-worker on the NAS - see CLAUDE.md). Poll instead of guess.
Write-Host "Waiting for RabbitMQ to accept connections..."
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    $test = Test-NetConnection -ComputerName localhost -Port 5672 -WarningAction SilentlyContinue
    if ($test.TcpTestSucceeded) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $ready) {
    Write-Host "RabbitMQ did not become ready within 30s - aborting." -ForegroundColor Red
    exit 1
}
Write-Host "RabbitMQ is ready."

$env:MediaSettings__SourceRoot  = $SourcePath
$env:MediaSettings__LibraryRoot = $LibraryPath
$env:ConnectionStrings__MediaDb = "Data Source=$DbPath"
$env:RabbitMq__Host             = "localhost"

# Build both entry projects up front, then run each with --no-build. Starting
# Worker and Web as two concurrent `dotnet run` processes used to race on
# compiling their shared dependency (ITMartin.Media.Runtime) into the same
# obj/bin output - CS2012 "Cannot open ...Runtime.dll for writing" whenever
# one process still had the DLL open while the other tried to overwrite it.
Write-Host "Building Worker and Web..."
dotnet build ITMartinFileSorter.Worker/ITMartinFileSorter.Worker.csproj
if ($LASTEXITCODE -ne 0) { Write-Host "Worker build failed - aborting." -ForegroundColor Red; exit 1 }
dotnet build ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj
if ($LASTEXITCODE -ne 0) { Write-Host "Web build failed - aborting." -ForegroundColor Red; exit 1 }

Write-Host "Starting FileSorter Worker..."
$worker = Start-Process -PassThru -NoNewWindow dotnet -ArgumentList "run --no-build --project ITMartinFileSorter.Worker/ITMartinFileSorter.Worker.csproj"

Write-Host "Starting FileSorter Web on http://localhost:8080 ..."
$web = Start-Process -PassThru -NoNewWindow dotnet -ArgumentList "run --no-build --project ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj --urls http://localhost:8080"

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

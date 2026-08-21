<#
.SYNOPSIS
    Real, irreversible duplicate removal within one folder's subtree - exact
    byte-identical matches (keeps one, deletes the rest) plus per-subfolder
    near-duplicate/recompressed matches (keeps the largest file). Never
    compares across the given folder's own boundary, so a deliberate
    SmartFolders copy sitting in a sibling folder is untouched.

.PARAMETER Path
    The folder whose subtree should be deduplicated, e.g. "E:\Rico\Images".

.EXAMPLE
    .\dedupe-folder.ps1 -Path "E:\Rico\Images"
#>

param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$Port = 5299
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) { Write-Error "Path not found: $Path"; exit 1 }
$fullPath = (Resolve-Path $Path).Path

$repoRoot = $PSScriptRoot
$scratchDb = Join-Path $env:TEMP "filesorter-check-scratch-$Port"
New-Item -ItemType Directory -Force -Path $scratchDb | Out-Null

$port = $Port
$baseUrl = "http://localhost:$port"

$env:MediaSettings__LibraryRoot = $scratchDb
Remove-Item Env:MediaSettings__SourceRoot -ErrorAction SilentlyContinue
Remove-Item Env:MediaSettings__ClientSlug -ErrorAction SilentlyContinue

Write-Host "Building FileSorter.Server..."
& dotnet build "$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj" --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Starting throwaway FileSorter.Server on $baseUrl ..."
$logFile = Join-Path $scratchDb "server.log"
$errFile = Join-Path $scratchDb "server.err.log"
$server = Start-Process -PassThru -WindowStyle Hidden dotnet `
    -ArgumentList "run --project `"$repoRoot\ITMartinFileSorter.Server\ITMartinFileSorter.Server.csproj`" --urls $baseUrl --no-launch-profile --no-build" `
    -RedirectStandardOutput $logFile -RedirectStandardError $errFile

try {
    $ready = $false
    $probeUri = "$baseUrl/api/debug/p4-verify-structure?path=$([uri]::EscapeDataString($scratchDb))"
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        try {
            Invoke-WebRequest -Uri $probeUri -Method Post -UseBasicParsing -TimeoutSec 10 | Out-Null
            $ready = $true
            break
        } catch {
            if ($_.Exception.Response) { $ready = $true; break }
            if ($server.HasExited) {
                Get-Content $logFile -ErrorAction SilentlyContinue | Write-Host
                Get-Content $errFile -ErrorAction SilentlyContinue | Write-Host
                Write-Error "FileSorter.Server exited before becoming ready."
                exit 1
            }
        }
    }
    if (-not $ready) { Write-Error "FileSorter.Server did not become ready in time."; exit 1 }

    Write-Host "Deduplicating: $fullPath (this deletes files - exact matches keep one copy, near-duplicates keep the largest)"
    $encodedPath = [uri]::EscapeDataString($fullPath)
    $result = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/debug/deduplicate-folder?path=$encodedPath" -TimeoutSec 3600

    Write-Host ""
    Write-Host "=== Deduplicate result for $fullPath ==="
    Write-Host "Checked: $($result.checked)"
    Write-Host "Deleted: $($result.deleted)"
}
finally {
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}

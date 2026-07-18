<#
    Starts musik-studio-web locally, after pulling the current state down from
    the NAS first - so a local session always starts from whatever was last
    saved there, regardless of drive-letter mounting (Y:\ etc.), which Docker
    Desktop's WSL2 backend can't reliably see anyway (see feedback memory:
    WSL2 only sees drives that existed when it started - a NAS SMB mount is
    exactly the kind of drive that trips this).

    Uses tar+scp rather than "scp -r" directly - scp -r's directory-copy
    semantics differ between Unix and Windows OpenSSH (risk of nesting a
    copy inside itself on repeat runs), whereas one tarball + extract is
    unambiguous and was already proven reliable earlier the same day this
    was written (see the SmartFolders symlink-transfer fix in receipt-web's
    sibling FileSorter work).

    Pair with stop-musikstudio.ps1, which pushes local changes back up when
    you're done. Running start without ever running stop just means the NAS
    copy goes stale until you do - it will not lose anything locally.
#>

$NasHost      = "martinhvidberg@100.117.120.44"
$NasMusicPath = "/volume1/MartinMusik"
$NasDataPath  = "/volume1/docker/martinsuite/musikstudio/data"
$LocalMusic   = "C:\MartinMusikLocal"
$LocalData    = "C:\MusikStudioLocalData"

function Pull-FromNas($remotePath, $localPath, $label) {
    Write-Host "Pulling $label from NAS..." -ForegroundColor Cyan

    $tarName = "musikstudio-pull-$($label -replace '\s','').tar.gz"
    $remoteTar = "/tmp/$tarName"
    $localTar  = "$env:TEMP\$tarName"

    ssh $NasHost "tar -czf $remoteTar -C $remotePath ."
    if ($LASTEXITCODE -ne 0) { Write-Error "$label - remote tar failed."; exit 1 }

    scp -O "${NasHost}:${remoteTar}" $localTar
    if ($LASTEXITCODE -ne 0) { Write-Error "$label - download failed."; exit 1 }

    ssh $NasHost "rm $remoteTar"

    # Fresh extract every time - this local copy is a disposable working copy,
    # so clearing it first avoids stale files piling up from things deleted
    # on the NAS side since the last pull.
    if (Test-Path $localPath) { Remove-Item -Path $localPath -Recurse -Force }
    New-Item -ItemType Directory -Path $localPath -Force | Out-Null

    tar --force-local -xzf $localTar -C $localPath
    if ($LASTEXITCODE -ne 0) { Write-Error "$label - local extract failed. $localPath may be empty - do not trust this pull, retry before starting the container."; exit 1 }
    Remove-Item $localTar -Force

    Write-Host "$label ready at $localPath" -ForegroundColor Green
}

Pull-FromNas $NasMusicPath $LocalMusic "music library"
Pull-FromNas $NasDataPath  $LocalData  "studio data"

Write-Host "Starting musik-studio-web..." -ForegroundColor Cyan
docker compose up -d musik-studio-web

Write-Host ""
Write-Host "Running at http://localhost:8105" -ForegroundColor Green
Write-Host "When done, run .\stop-musikstudio.ps1 to push everything back to the NAS." -ForegroundColor Yellow

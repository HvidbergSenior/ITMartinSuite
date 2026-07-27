<#
    Stops musik-studio-web and pushes local changes (recordings, versions,
    stems, the studio.db) back up to the NAS, so nothing created in this
    session stays stranded on this laptop. Pair with start-musikstudio.ps1.

    Pushes are a merge/update onto the NAS folder, not a mirror - tar
    extraction overwrites matching files but never deletes anything on the
    NAS side that isn't in the local copy. That's deliberate: this is the
    master copy, so it's safer to never auto-delete from it, even if that
    means a file you removed locally has to be deleted on the NAS by hand.
#>

$NasHost       = "martinhvidberg@100.117.120.44"
$NasMusicPath  = "/volume1/MartinMusik"
$NasDataPath   = "/volume1/docker/martinsuite/musikstudio/data"
$NasStagingDir = "/volume1/docker/martinsuite/tmp-transfer"
$LocalMusic    = "C:\MartinMusikLocal"
$LocalData     = "C:\MusikStudioLocalData"

function Push-ToNas($localPath, $remotePath, $label) {
    Write-Host "Pushing $label to NAS..." -ForegroundColor Cyan

    $tarName   = "musikstudio-push-$($label -replace '\s','').tar.gz"
    $localTar  = "$env:TEMP\$tarName"
    # /tmp on this NAS is a RAM-backed tmpfs (~866M total, shared with the
    # rest of the box's 1.7GB) - staging a music-library-sized tarball there
    # (via upload + extract) took the whole NAS down twice. Stage on
    # /volume1 instead (real disk, 8+TB free) - a tarball orphaned here by an
    # interrupted transfer just wastes disk space, not RAM the box needs.
    $remoteTar = "$NasStagingDir/$tarName"

    ssh $NasHost "mkdir -p $NasStagingDir && rm -f $remoteTar" | Out-Null

    # Windows' built-in tar.exe is bsdtar, not GNU tar - it has no
    # --force-local flag at all (that was a GNU-tar-only fix for colon
    # ambiguity in "C:\..." paths); bsdtar doesn't have that ambiguity.
    tar -czf $localTar -C $localPath .
    if ($LASTEXITCODE -ne 0) { Write-Error "$label - local tar failed. Local copy at $localPath is untouched."; exit 1 }

    scp -O $localTar "${NasHost}:${remoteTar}"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$label - upload failed. Local copy at $localPath is untouched, retry this script."
        ssh $NasHost "rm -f $remoteTar" | Out-Null
        exit 1
    }
    Remove-Item $localTar -Force

    ssh $NasHost "tar -xzf $remoteTar -C $remotePath && rm $remoteTar"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$label - remote extract failed."
        ssh $NasHost "rm -f $remoteTar" | Out-Null
        exit 1
    }

    Write-Host "$label pushed." -ForegroundColor Green
}

Write-Host "Stopping musik-studio-web..." -ForegroundColor Cyan
docker compose stop musik-studio-web

Push-ToNas $LocalMusic $NasMusicPath "music library"
Push-ToNas $LocalData  $NasDataPath  "studio data"

Write-Host "Done - NAS is up to date." -ForegroundColor Green

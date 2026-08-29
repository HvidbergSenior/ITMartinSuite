<#
.SYNOPSIS
  Gate check: run the gallery UI tests against a freshly-sorted library and
  refuse to let you proceed if the gallery is broken.

.DESCRIPTION
  Run this after Package1/Package3 finish on C: and BEFORE copying/syncing
  the result out to an external HD or the NAS. Catches exactly the class of
  bug found 2026-08-28 - missing thumbnails, every video silently undated -
  before it gets copied everywhere and someone has to notice it by eye.

.PARAMETER LibraryPath
  The library root to test (must contain _Galleri and index.html - i.e. a
  Package3 export has already run against it). Defaults to C:\FileSorterOutput,
  adjust per real local convention.

.EXAMPLE
  .\run-before-copy.ps1 -LibraryPath "C:\FileSorterOutput\mie"
  # then only if this exits 0:
  robocopy "C:\FileSorterOutput\mie" "D:\MieFiler" /E /R:2 /W:2 /NFL /NDL /NP
#>
param(
    [string]$LibraryPath = "C:\FileSorterOutput"
)

if (-not (Test-Path (Join-Path $LibraryPath "_Galleri"))) {
    Write-Error "No _Galleri folder under '$LibraryPath' - run the Package3/gallery export first."
    exit 1
}

$env:GALLERY_TEST_LIBRARY_PATH = $LibraryPath

Write-Output "Running gallery UI tests against: $LibraryPath"
Write-Output "----------------------------------------------------------------"

$testProject = Join-Path $PSScriptRoot "ITMartin.GalleryUi.Tests.csproj"
dotnet test $testProject -c Release --logger "console;verbosity=normal"

if ($LASTEXITCODE -ne 0) {
    Write-Output ""
    Write-Output "----------------------------------------------------------------"
    Write-Error "Gallery UI tests FAILED - do not copy this library out yet. Fix the failure above first."
    exit $LASTEXITCODE
}

Write-Output ""
Write-Output "----------------------------------------------------------------"
Write-Output "Gallery UI tests passed - safe to copy '$LibraryPath' out now."

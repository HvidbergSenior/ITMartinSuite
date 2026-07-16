<#
.SYNOPSIS
    Verifies a finished library copy (e.g. onto a customer's external hard drive)
    exactly matches the source before you consider a delivery done and unplug it.

.PARAMETER Source
    The working library folder (what was supposed to be copied).

.PARAMETER Destination
    Where it was copied to (e.g. an external drive letter).

.PARAMETER Quick
    Skip SHA256 hashing - only compares file presence + size. Fast, good for a
    first pass right after a copy. Omit this switch for the final check before
    handover: full SHA256 hashing catches silent corruption that size alone won't.

.EXAMPLE
    .\verify-delivery.ps1 -Source "C:\FileSorterTests\MieCarstenLibraryOutput\mie" -Destination "E:\Mie" -Quick
    .\verify-delivery.ps1 -Source "C:\FileSorterTests\MieCarstenLibraryOutput\mie" -Destination "E:\Mie"
#>

param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$Quick
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) {
    Write-Error "Source path not found: $Source"
    exit 1
}
if (-not (Test-Path $Destination)) {
    Write-Error "Destination path not found: $Destination"
    exit 1
}

$sourceFull = (Resolve-Path $Source).Path
$destFull = (Resolve-Path $Destination).Path

Write-Host "Scanning source: $sourceFull"
$sourceFiles = Get-ChildItem -Path $sourceFull -Recurse -File
Write-Host "Scanning destination: $destFull"
$destFiles = Get-ChildItem -Path $destFull -Recurse -File

$sourceMap = @{}
foreach ($f in $sourceFiles) {
    $rel = $f.FullName.Substring($sourceFull.Length).TrimStart('\', '/')
    $sourceMap[$rel] = $f
}
$destMap = @{}
foreach ($f in $destFiles) {
    $rel = $f.FullName.Substring($destFull.Length).TrimStart('\', '/')
    $destMap[$rel] = $f
}

$missing = New-Object System.Collections.Generic.List[string]
$sizeMismatch = New-Object System.Collections.Generic.List[string]
$hashMismatch = New-Object System.Collections.Generic.List[string]
$extra = New-Object System.Collections.Generic.List[string]
$matched = 0
$checked = 0
$total = $sourceMap.Count

foreach ($rel in $sourceMap.Keys) {
    $checked++
    if ($checked % 500 -eq 0) {
        Write-Host "Checked $checked / $total"
    }

    $srcFile = $sourceMap[$rel]

    if (-not $destMap.ContainsKey($rel)) {
        $missing.Add($rel)
        continue
    }

    $dstFile = $destMap[$rel]

    if ($srcFile.Length -ne $dstFile.Length) {
        $sizeMismatch.Add($rel)
        continue
    }

    if (-not $Quick) {
        $srcHash = (Get-FileHash -Path $srcFile.FullName -Algorithm SHA256).Hash
        $dstHash = (Get-FileHash -Path $dstFile.FullName -Algorithm SHA256).Hash
        if ($srcHash -ne $dstHash) {
            $hashMismatch.Add($rel)
            continue
        }
    }

    $matched++
}

foreach ($rel in $destMap.Keys) {
    if (-not $sourceMap.ContainsKey($rel)) {
        $extra.Add($rel)
    }
}

$mode = "Full (SHA256 hash)"
if ($Quick) { $mode = "Quick (size only)" }

Write-Host ""
Write-Host "=== Delivery Verification Report ==="
Write-Host "Mode:              $mode"
Write-Host "Source files:      $($sourceMap.Count)"
Write-Host "Destination files: $($destMap.Count)"
Write-Host "Matched OK:        $matched"
Write-Host "Missing in dest:   $($missing.Count)"
Write-Host "Size mismatches:   $($sizeMismatch.Count)"
if (-not $Quick) {
    Write-Host "Hash mismatches:   $($hashMismatch.Count)"
}
Write-Host "Extra in dest:     $($extra.Count)"

$reportPath = Join-Path $destFull "delivery-verification-report.txt"
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Delivery Verification Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$lines.Add("Source: $sourceFull")
$lines.Add("Destination: $destFull")
$lines.Add("Mode: $mode")
$lines.Add("")
$lines.Add("Source files:      $($sourceMap.Count)")
$lines.Add("Destination files: $($destMap.Count)")
$lines.Add("Matched OK:        $matched")
$lines.Add("Missing in dest:   $($missing.Count)")
$lines.Add("Size mismatches:   $($sizeMismatch.Count)")
if (-not $Quick) { $lines.Add("Hash mismatches:   $($hashMismatch.Count)") }
$lines.Add("Extra in dest:     $($extra.Count)")

if ($missing.Count -gt 0) {
    $lines.Add("")
    $lines.Add("--- Missing files ---")
    foreach ($m in $missing) { $lines.Add($m) }
}
if ($sizeMismatch.Count -gt 0) {
    $lines.Add("")
    $lines.Add("--- Size mismatches ---")
    foreach ($m in $sizeMismatch) { $lines.Add($m) }
}
if ((-not $Quick) -and ($hashMismatch.Count -gt 0)) {
    $lines.Add("")
    $lines.Add("--- Hash mismatches (corruption) ---")
    foreach ($m in $hashMismatch) { $lines.Add($m) }
}
if ($extra.Count -gt 0) {
    $lines.Add("")
    $lines.Add("--- Extra files in destination (not in source) ---")
    foreach ($m in $extra) { $lines.Add($m) }
}

$lines | Set-Content -Path $reportPath -Encoding UTF8
Write-Host ""
Write-Host "Full report written to: $reportPath"

if (($missing.Count -gt 0) -or ($sizeMismatch.Count -gt 0) -or ($hashMismatch.Count -gt 0)) {
    Write-Host ""
    Write-Host "RESULT: FAILED - delivery is incomplete or corrupted. Do not consider this handover done yet." -ForegroundColor Red
    exit 1
}
else {
    Write-Host ""
    Write-Host "RESULT: PASSED - every source file is present and verified on the destination." -ForegroundColor Green
    exit 0
}

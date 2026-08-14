<#
.SYNOPSIS
Builds a small, self-contained, reproducible test source folder covering every
media category FileSorter's Package1 recognizes (image, video, audio, document,
zip archive), plus a couple of deliberate filename collisions - used by
test-pipeline-e2e.ps1 to exercise the whole sort pipeline without needing any
real customer data or external downloads.

.PARAMETER OutputRoot
Directory to (re)create the fixture in. Wiped and rebuilt fresh every call so
re-running the e2e test always starts from a known, identical source folder.

.OUTPUTS
A manifest object describing every top-level file placed in the source folder
and which Package1 category it's expected to land in after sorting - the
caller uses this to assert the pipeline actually did the right thing.
#>
param(
    [Parameter(Mandatory)]
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (Test-Path $OutputRoot) {
    Remove-Item -Recurse -Force $OutputRoot
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$ffmpeg = Join-Path (Split-Path $PSScriptRoot -Parent) "..\ITMartinFileSorter.Server\ffmpeg\ffmpeg.exe" | Resolve-Path -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    $ffmpeg = Join-Path $PSScriptRoot "..\..\ITMartinFileSorter.Server\ffmpeg\ffmpeg.exe"
    $ffmpeg = (Resolve-Path $ffmpeg).Path
}

# --- helpers -----------------------------------------------------------

function New-JpgWithExifDate {
    param([string]$Path, [int]$Width, [int]$Height, [System.Drawing.Color]$Color, [datetime]$Date)

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($Color)
    $g.Dispose()

    # System.Drawing.Imaging.PropertyItem has no public constructor - the
    # standard trick is to invoke its non-public parameterless ctor via
    # reflection so we can attach a real EXIF DateTimeOriginal (tag 0x9003)
    # before saving, letting the fixture exercise date-based Year/Month sort.
    $piCtor = [System.Drawing.Imaging.PropertyItem].GetConstructor(
        [System.Reflection.BindingFlags]'NonPublic,Instance', $null, @(), $null)
    $pi = $piCtor.Invoke($null)
    $pi.Id = 0x9003
    $pi.Type = 2
    $dateStr = $Date.ToString("yyyy:MM:dd HH:mm:ss") + "`0"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($dateStr)
    $pi.Len = $bytes.Length
    $pi.Value = $bytes
    $bmp.SetPropertyItem($pi)

    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $bmp.Dispose()
}

function New-TestVideo {
    # Duration must clear CleanupEvaluationWorkflowStep's < 3s DeleteCandidate
    # threshold, or the clip gets (correctly) routed to DeleteCandidates
    # instead of Videos - use 4s to be safely above it. -metadata creation_time
    # is what MetadataWorkflowStep actually reads via ffprobe (format_tags/
    # stream_tags creation_time) - without it the clip has no trustworthy date
    # at all and lands in Undated/Videos instead of a real Year/Month folder.
    param([string]$Path, [string]$Color = "red", [datetime]$Date)
    $iso = $Date.ToString("yyyy-MM-ddTHH:mm:ss.000000Z")
    & $ffmpeg -y -loglevel error -f lavfi -i "color=c=${Color}:size=320x240:duration=4:rate=10" `
        -pix_fmt yuv420p -metadata creation_time=$iso -movflags use_metadata_tags $Path
}

function New-TestAudio {
    # Same >= 3s DeleteCandidate threshold applies to any file with a
    # Duration set, audio included - not just video.
    param([string]$Path)
    & $ffmpeg -y -loglevel error -f lavfi -i "sine=frequency=440:duration=4" $Path
}

function New-MinimalPdf {
    param([string]$Path)
    # Smallest valid single-page PDF skeleton - no external library needed.
    # PDFs get no date extraction at all in this pipeline (DocumentMetadataService
    # only reads docProps/core.xml from .docx/.xlsx/.pptx), so a plain PDF like
    # this always lands in Undated/Documents - useful as exactly that test case,
    # not a bug to fix here.
    $pdf = @"
%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj
xref
0 4
0000000000 65535 f
trailer<</Size 4/Root 1 0 R>>
startxref
0
%%EOF
"@
    Set-Content -Path $Path -Value $pdf -Encoding ASCII -NoNewline
}

function New-MinimalDocxWithDate {
    # Minimal valid .docx (OOXML zip) with a real docProps/core.xml created
    # date - DocumentMetadataService.GetCreationTime reads exactly this
    # (dcterms:created), so this is what a demo/showcase document needs to
    # land in a real Year/Month folder instead of Undated/Documents.
    param([string]$Path, [datetime]$Date, [string]$Text = "End-to-end test fixture document.")

    if (Test-Path $Path) { Remove-Item $Path -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $created = $Date.ToString("yyyy-MM-ddTHH:mm:ssZ")
    $contentTypes = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>'
    $rels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>'
    $document = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>' + $Text + '</w:t></w:r></w:p></w:body></w:document>'
    $core = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dcterms:created xsi:type="dcterms:W3CDTF">' + $created + '</dcterms:created></cp:coreProperties>'

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    $archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in @(
            @{ Name = "[Content_Types].xml"; Content = $contentTypes },
            @{ Name = "_rels/.rels"; Content = $rels },
            @{ Name = "word/document.xml"; Content = $document },
            @{ Name = "docProps/core.xml"; Content = $core }
        )) {
            $zipEntry = $archive.CreateEntry($entry.Name)
            $entryStream = $zipEntry.Open()
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($entry.Content)
            $entryStream.Write($bytes, 0, $bytes.Length)
            $entryStream.Close()
        }
    } finally {
        $archive.Dispose()
        $fs.Close()
    }
}

# --- build fixture -------------------------------------------------------

$manifest = @()

# Images - 3 different years so date-based Year/Month sorting is actually exercised
$img1 = Join-Path $OutputRoot "photo_2021.jpg"
New-JpgWithExifDate -Path $img1 -Width 300 -Height 300 -Color ([System.Drawing.Color]::CornflowerBlue) -Date (Get-Date "2021-06-15")
$manifest += [PSCustomObject]@{ File = "photo_2021.jpg"; Category = "Images" }

$img2 = Join-Path $OutputRoot "photo_2023.jpg"
New-JpgWithExifDate -Path $img2 -Width 300 -Height 300 -Color ([System.Drawing.Color]::SeaGreen) -Date (Get-Date "2023-11-02")
$manifest += [PSCustomObject]@{ File = "photo_2023.jpg"; Category = "Images" }

$img3 = Join-Path $OutputRoot "photo_2024.jpg"
New-JpgWithExifDate -Path $img3 -Width 300 -Height 300 -Color ([System.Drawing.Color]::Goldenrod) -Date (Get-Date "2024-01-20")
$manifest += [PSCustomObject]@{ File = "photo_2024.jpg"; Category = "Images" }

# Videos - dated via ffmpeg's creation_time metadata, same reasoning as the
# images' EXIF dates.
$vid1 = Join-Path $OutputRoot "clip_a.mp4"
New-TestVideo -Path $vid1 -Color "red" -Date (Get-Date "2022-04-10")
$manifest += [PSCustomObject]@{ File = "clip_a.mp4"; Category = "Videos" }

$vid2 = Join-Path $OutputRoot "clip_b.mp4"
New-TestVideo -Path $vid2 -Color "blue" -Date (Get-Date "2023-09-22")
$manifest += [PSCustomObject]@{ File = "clip_b.mp4"; Category = "Videos" }

# Audio (.wav, not .mp3 - avoids depending on ffmpeg having libmp3lame built in;
# .wav is already a recognized audio extension in MediaTypeHelper)
$audio1 = Join-Path $OutputRoot "tone.wav"
New-TestAudio -Path $audio1
$manifest += [PSCustomObject]@{ File = "tone.wav"; Category = "Musik" }

# Documents - one dated .docx (real docProps/core.xml date, lands in a real
# Year/Month folder) and one plain .pdf (deliberately undated - PDFs get no
# date extraction in this pipeline at all, so it correctly lands in
# Undated/Documents instead).
$doc1 = Join-Path $OutputRoot "report_2023.docx"
New-MinimalDocxWithDate -Path $doc1 -Date (Get-Date "2023-03-12")
$manifest += [PSCustomObject]@{ File = "report_2023.docx"; Category = "Documents" }

$doc2 = Join-Path $OutputRoot "report.pdf"
New-MinimalPdf -Path $doc2
$manifest += [PSCustomObject]@{ File = "report.pdf"; Category = "Documents" }

# Zip archive - bundles one image + one document, proves FileScanner's
# TryEnsureZipExtracted path actually runs and the extracted contents flow
# through the normal per-file pipeline like any other source file.
$zipStaging = Join-Path $OutputRoot "_zip_staging"
New-Item -ItemType Directory -Force -Path $zipStaging | Out-Null
New-JpgWithExifDate -Path (Join-Path $zipStaging "zipped_photo.jpg") -Width 300 -Height 300 -Color ([System.Drawing.Color]::Crimson) -Date (Get-Date "2022-08-09")
Set-Content -Path (Join-Path $zipStaging "zipped_notes.txt") -Value "Came from inside a zip." -Encoding UTF8
$zipPath = Join-Path $OutputRoot "bundle.zip"
Compress-Archive -Path (Join-Path $zipStaging "*") -DestinationPath $zipPath
Remove-Item -Recurse -Force $zipStaging
$manifest += [PSCustomObject]@{ File = "zipped_photo.jpg"; Category = "Images"; FromZip = $true }
$manifest += [PSCustomObject]@{ File = "zipped_notes.txt"; Category = "Documents"; FromZip = $true }

# Deliberate filename collision - same filename, different content, different
# subfolders, so both end up needing EnsureUniqueFileName's collision handling
# on export rather than each other overwriting.
$dupDirA = Join-Path $OutputRoot "batch_a"
$dupDirB = Join-Path $OutputRoot "batch_b"
New-Item -ItemType Directory -Force -Path $dupDirA, $dupDirB | Out-Null
New-JpgWithExifDate -Path (Join-Path $dupDirA "IMG_0001.jpg") -Width 300 -Height 300 -Color ([System.Drawing.Color]::Orange) -Date (Get-Date "2020-03-03")
New-JpgWithExifDate -Path (Join-Path $dupDirB "IMG_0001.jpg") -Width 300 -Height 300 -Color ([System.Drawing.Color]::Purple) -Date (Get-Date "2020-03-04")
$manifest += [PSCustomObject]@{ File = "IMG_0001.jpg"; Category = "Images"; Collision = $true }
$manifest += [PSCustomObject]@{ File = "IMG_0001.jpg"; Category = "Images"; Collision = $true }

return $manifest

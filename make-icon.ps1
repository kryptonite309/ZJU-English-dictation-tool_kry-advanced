param(
    [string]$InputPath = ".\assets\app-icon-source.png",
    [string]$PngOutputPath = ".\app-icon.png",
    [string]$IcoOutputPath = ".\app.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$input = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $InputPath))
$pngOutput = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $PngOutputPath))
$icoOutput = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $IcoOutputPath))

function New-IconBitmap([System.Drawing.Image]$source, [int]$size, [int]$padding) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size,
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $contentSize = $size - (2 * $padding)
        $destination = New-Object System.Drawing.Rectangle $padding, $padding, $contentSize, $contentSize
        $graphics.DrawImage($source, $destination, 0, 0, $source.Width, $source.Height,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function Convert-BitmapToPngBytes([System.Drawing.Image]$image) {
    $stream = New-Object System.IO.MemoryStream
    try {
        $image.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

$source = [System.Drawing.Image]::FromFile($input)
try {
    $preview = New-IconBitmap $source 1024 46
    try {
        $preview.Save($pngOutput, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $preview.Dispose()
    }
}
finally {
    $source.Dispose()
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$pngImages = New-Object System.Collections.Generic.List[byte[]]
$prepared = [System.Drawing.Image]::FromFile($pngOutput)
try {
    foreach ($size in $sizes) {
        $bitmap = New-IconBitmap $prepared $size 0
        try {
            $pngImages.Add((Convert-BitmapToPngBytes $bitmap))
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $prepared.Dispose()
}

$fileStream = [System.IO.File]::Open($icoOutput, [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$writer = New-Object System.IO.BinaryWriter $fileStream
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $bytes = $pngImages[$index]
        $dimension = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $bytes.Length
    }

    foreach ($bytes in $pngImages) {
        $writer.Write($bytes)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Get-Item -LiteralPath $pngOutput, $icoOutput

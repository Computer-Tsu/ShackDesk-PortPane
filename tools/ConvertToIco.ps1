# ConvertToIco.ps1
# Converts a PNG to a multi-resolution Windows .ico file.
# Usage: .\tools\ConvertToIco.ps1 -Source docs\assets\PP-v1.png -Dest docs\assets\icon.ico
param(
    [string]$Source = "docs\assets\PP-v1.png",
    [string]$Dest   = "docs\assets\icon.ico"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 64, 128, 256)

$sourcePath = Resolve-Path $Source
$destPath   = Join-Path (Get-Location) $Dest

$original = [System.Drawing.Image]::FromFile($sourcePath)

# ICO file format:
#   6-byte header  : reserved(2), type=1(2), count(2)
#   16-byte entry per image: width(1), height(1), colorCount(1), reserved(1),
#                            planes(2), bitCount(2), bytesInRes(4), imageOffset(4)
#   PNG blob per image (modern ICO stores raw PNG for sizes >= 256; BMP DIB for smaller)

$images  = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($original, 0, 0, $size, $size)
    $g.Dispose()

    $ms = [System.IO.MemoryStream]::new()
    if ($size -ge 256) {
        # Store as PNG blob (transparent, compact)
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Store as 32bpp BMP DIB (no file header — ICO embeds the DIB directly)
        # Build DIB manually: BITMAPINFOHEADER + XOR mask (ARGB rows, bottom-up) + no AND mask
        $dibHeader = [byte[]]::new(40)
        $w = $size; $h = $size
        [BitConverter]::GetBytes([int32]40).CopyTo($dibHeader, 0)   # biSize
        [BitConverter]::GetBytes([int32]$w).CopyTo($dibHeader, 4)   # biWidth
        [BitConverter]::GetBytes([int32]($h * 2)).CopyTo($dibHeader, 8)  # biHeight (doubled for ICO)
        [BitConverter]::GetBytes([int16]1).CopyTo($dibHeader, 12)  # biPlanes
        [BitConverter]::GetBytes([int16]32).CopyTo($dibHeader, 14) # biBitCount
        # remaining fields stay 0 (BI_RGB, sizes computed by reader)
        $ms.Write($dibHeader, 0, 40)

        # Pixel data: 32bpp BGRA, bottom-up row order
        $bmpData = $bmp.LockBits(
            [System.Drawing.Rectangle]::new(0, 0, $w, $h),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        $rowBytes = $w * 4
        $pixelBuf = [byte[]]::new($rowBytes)
        for ($row = ($h - 1); $row -ge 0; $row--) {
            $ptr = [IntPtr]($bmpData.Scan0.ToInt64() + $row * $bmpData.Stride)
            [System.Runtime.InteropServices.Marshal]::Copy($ptr, $pixelBuf, 0, $rowBytes)
            $ms.Write($pixelBuf, 0, $rowBytes)
        }
        $bmp.UnlockBits($bmpData)
    }

    $bmp.Dispose()
    $images.Add($ms.ToArray())
    $ms.Dispose()
}

$original.Dispose()

# Write ICO file
$out = [System.IO.FileStream]::new($destPath, [System.IO.FileMode]::Create)
$w16 = { param($v) $out.Write([BitConverter]::GetBytes([int16]$v), 0, 2) }
$w32 = { param($v) $out.Write([BitConverter]::GetBytes([int32]$v), 0, 4) }

# Header
& $w16 0              # reserved
& $w16 1              # type = icon
& $w16 $sizes.Count   # image count

# Directory entries — calculate offsets
$dataOffset = 6 + ($sizes.Count * 16)
$offsets = @()
$offset  = $dataOffset
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $offsets += $offset
    $offset  += $images[$i].Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $out.WriteByte($(if ($sz -ge 256) { 0 } else { $sz }))  # width  (0 = 256)
    $out.WriteByte($(if ($sz -ge 256) { 0 } else { $sz }))  # height (0 = 256)
    $out.WriteByte(0)                                         # colorCount (0 = >256 colors)
    $out.WriteByte(0)                                         # reserved
    & $w16 1                                                  # planes
    & $w16 32                                                 # bitCount
    & $w32 $images[$i].Length                                 # bytesInRes
    & $w32 $offsets[$i]                                       # imageOffset
}

# Image data
foreach ($blob in $images) {
    $out.Write($blob, 0, $blob.Length)
}

$out.Close()

Write-Host "Written: $destPath"
Write-Host "Sizes:   $($sizes -join ', ')px"
Write-Host "Total:   $([Math]::Round((Get-Item $destPath).Length / 1KB, 1)) KB"

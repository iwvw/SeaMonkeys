Add-Type -AssemblyName System.Drawing

$src = "E:\Code\SeaMonkeys\src\SeaMonkeys.App\Assets\stamps\seamonkey.png"
$outDir = "E:\Code\SeaMonkeys\src\SeaMonkeys.App\Assets"

# 先把源印章裁剪到内容边界（去掉透明留白），再缩放，使图标内容更饱满。
$raw = [System.Drawing.Bitmap]::FromFile($src)
$minX = $raw.Width; $minY = $raw.Height; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $raw.Height; $y++) {
    for ($x = 0; $x -lt $raw.Width; $x++) {
        if ($raw.GetPixel($x, $y).A -gt 16) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$pad = 6
$minX = [Math]::Max(0, $minX - $pad); $minY = [Math]::Max(0, $minY - $pad)
$maxX = [Math]::Min($raw.Width - 1, $maxX + $pad); $maxY = [Math]::Min($raw.Height - 1, $maxY + $pad)
$rect = New-Object System.Drawing.Rectangle($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
$stamp = $raw.Clone($rect, $raw.PixelFormat)
$raw.Dispose()

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # 内容占满（98%），并整体轻微左旋。
    $inner = [int]($size * 0.98)
    $off = [int](($size - $inner) / 2)

    $g.TranslateTransform($size / 2.0, $size / 2.0)
    $g.RotateTransform(-8.0)
    $g.TranslateTransform(-$size / 2.0, -$size / 2.0)

    # 圆内白底（略小于圆环内侧）
    $padW = [int]($inner * 0.055)
    $g.FillEllipse([System.Drawing.Brushes]::White, $off + $padW, $off + $padW, $inner - 2 * $padW, $inner - 2 * $padW)

    # 叠加印章
    $g.DrawImage($stamp, $off, $off, $inner, $inner)
    $g.Dispose()
    return $bmp
}

$png = New-IconBitmap 512
$png.Save((Join-Path $outDir "AppIcon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += New-IconBitmap $s }

$icoPath = Join-Path $outDir "AppIcon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)

$pngData = @()
foreach ($b in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData += ,$ms.ToArray()
    $ms.Dispose()
}

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngData[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngData[$i].Length
}
foreach ($d in $pngData) { $bw.Write($d) }
$bw.Flush(); $bw.Close(); $fs.Close()

foreach ($b in $bitmaps) { $b.Dispose() }
$stamp.Dispose()

Write-Output "generated:"
Get-ChildItem $outDir -Filter "AppIcon.*" | Select-Object Name, Length

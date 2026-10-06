Add-Type -AssemblyName System.Drawing

$outDir = "E:\Code\SeaMonkeys\src\SeaMonkeys.App\Assets\stamps"
$size = 256

function New-Stamp {
    param(
        [string]$File,
        [string[]]$Lines,
        [int[]]$Color
    )

    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $c = [System.Drawing.Color]::FromArgb(255, $Color[0], $Color[1], $Color[2])

    # 仅一条外圈粗环
    $pen = New-Object System.Drawing.Pen($c, 13)
    $m = 24
    $g.DrawEllipse($pen, $m, $m, $size - 2 * $m, $size - 2 * $m)

    $totalChars = ($Lines -join "").Length
    $fontSize = if ($totalChars -le 1) { 96 } elseif ($totalChars -eq 2) { 70 } else { 50 }
    $font = New-Object System.Drawing.Font("Microsoft YaHei", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush($c)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $text = ($Lines -join "`n")
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString($text, $font, $brush, $rect, $sf)
    $g.Dispose()

    # 做旧：随机擦除像素，产生斑驳（强度减弱）
    $rnd = New-Object System.Random(20261004)
    for ($i = 0; $i -lt 14000; $i++) {
        $x = $rnd.Next(0, $size)
        $y = $rnd.Next(0, $size)
        $px = $bmp.GetPixel($x, $y)
        if ($px.A -gt 0) {
            $r = $rnd.NextDouble()
            if ($r -lt 0.4) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $px.R, $px.G, $px.B))
            } elseif ($r -lt 0.7) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int]($px.A * 0.4), $px.R, $px.G, $px.B))
            }
        }
    }

    $bmp.Save($File, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# 颜色对齐分类色：神佬紫、大佬青、诗人翠绿、正常绿、路边一条黄、区红
New-Stamp -File (Join-Path $outDir "god.png")      -Lines @("神佬")      -Color @(160, 13, 197)
New-Stamp -File (Join-Path $outDir "pro.png")      -Lines @("大佬")      -Color @(2, 201, 179)
New-Stamp -File (Join-Path $outDir "poet.png")     -Lines @("诗人")      -Color @(31, 199, 90)
New-Stamp -File (Join-Path $outDir "normal.png")   -Lines @("过关")      -Color @(68, 179, 0)
New-Stamp -File (Join-Path $outDir "roadside.png") -Lines @("路边")      -Color @(255, 199, 31)
New-Stamp -File (Join-Path $outDir "seamonkey.png") -Lines @("区")       -Color @(254, 14, 0)
# 隐藏档案：棍木（与灰条同色）
New-Stamp -File (Join-Path $outDir "hidden.png")   -Lines @("棍木")      -Color @(136, 136, 136)

Write-Output "generated:"
Get-ChildItem $outDir -Filter *.png | Select-Object Name, Length

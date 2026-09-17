# Generates assets/picky.ico from code - no external tools, no image files.
# Mark: a link arriving from below and splitting into two routed arrows.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root "assets"
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }
$icoPath = Join-Path $assets "picky.ico"

$Accent  = [System.Drawing.ColorTranslator]::FromHtml("#8AB4F8")
$BgTop   = [System.Drawing.ColorTranslator]::FromHtml("#2A2C2F")
$BgBot   = [System.Drawing.ColorTranslator]::FromHtml("#191A1C")
$Border  = [System.Drawing.ColorTranslator]::FromHtml("#3C4043")

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x,           $y,           $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y,           $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d,   0, 90)
    $p.AddArc($x,           $y + $h - $d, $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

function Render-Icon([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $k = $S / 256.0   # design on a 256 grid, scale down

    # --- rounded background ---
    $inset  = 6 * $k
    $radius = [Math]::Max(2.0, 54 * $k)
    $path = New-RoundedPath $inset $inset ($S - $inset * 2) ($S - $inset * 2) $radius
    $rect = New-Object System.Drawing.RectangleF(0, 0, $S, $S)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect, $BgTop, $BgBot, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($brush, $path)

    if ($S -ge 32) {
        $bw = [Math]::Max(1.0, 3 * $k)
        $pen = New-Object System.Drawing.Pen($Border, $bw)
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }
    $brush.Dispose(); $path.Dispose()

    # --- the split-arrow mark ---
    # Stroke stays chunky at small sizes so it survives 16px.
    $w = if ($S -le 20) { [Math]::Max(2.0, 30 * $k) } else { 26 * $k }

    $pen = New-Object System.Drawing.Pen($Accent, $w)
    $pen.StartCap  = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap    = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor
    $pen.LineJoin  = [System.Drawing.Drawing2D.LineJoin]::Round

    $originX = 128 * $k; $originY = 202 * $k
    $forkY   = 134 * $k
    $tipY    =  74 * $k
    $leftX   =  70 * $k
    $rightX  = 186 * $k

    $left  = @(
        (New-Object System.Drawing.PointF($originX, $originY)),
        (New-Object System.Drawing.PointF($originX, $forkY)),
        (New-Object System.Drawing.PointF($leftX,   $tipY))
    )
    $right = @(
        (New-Object System.Drawing.PointF($originX, $originY)),
        (New-Object System.Drawing.PointF($originX, $forkY)),
        (New-Object System.Drawing.PointF($rightX,  $tipY))
    )
    $g.DrawLines($pen, [System.Drawing.PointF[]]$left)
    $g.DrawLines($pen, [System.Drawing.PointF[]]$right)
    $pen.Dispose()

    $g.Dispose()
    return $bmp
}

# --- encode each size to PNG, then pack an ICO container ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$blobs = @()
foreach ($s in $sizes) {
    $bmp = Render-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $blobs += ,@($s, $ms.ToArray())
    $ms.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0)               # reserved
$bw.Write([UInt16]1)               # type: icon
$bw.Write([UInt16]$blobs.Count)

$offset = 6 + (16 * $blobs.Count)
foreach ($b in $blobs) {
    $s = $b[0]; $data = $b[1]
    $dim = 0                              # 0 means 256 in the ICO directory
    if ($s -lt 256) { $dim = $s }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)             # palette
    $bw.Write([Byte]0)             # reserved
    $bw.Write([UInt16]1)           # colour planes
    $bw.Write([UInt16]32)          # bits per pixel
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($b in $blobs) { $bw.Write($b[1]) }

$bw.Flush(); $bw.Dispose(); $fs.Dispose()

$len = (Get-Item $icoPath).Length
Write-Host "ICO written: $icoPath ($len bytes, $($blobs.Count) sizes: $($sizes -join ', '))"

# Also emit a PNG of the mark for the README and release notes.
$docs = Join-Path $root "docs"
if (-not (Test-Path $docs)) { New-Item -ItemType Directory -Path $docs | Out-Null }
$logoPath = Join-Path $docs "logo.png"
$logo = Render-Icon 256
$logo.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Host "PNG written: $logoPath (256x256)"

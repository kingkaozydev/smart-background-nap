param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "assets")
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$pngPath = Join-Path $OutputDirectory "smart-background-nap.png"
$icoPath = Join-Path $OutputDirectory "smart-background-nap.ico"

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 256.0
    function S([float]$v) { return [float]($v * $scale) }

    $bgRect = New-Object System.Drawing.RectangleF (S 20), (S 20), (S 216), (S 216)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $radius = S 46
    $diameter = $radius * 2
    $path.AddArc($bgRect.X, $bgRect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($bgRect.Right - $diameter, $bgRect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($bgRect.Right - $diameter, $bgRect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bgRect.X, $bgRect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(255, 15, 18, 22)), ([System.Drawing.Color]::FromArgb(255, 38, 43, 51)), 45
    $g.FillPath($bgBrush, $path)
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 82, 91, 105)), (S 5)
    $g.DrawPath($borderPen, $path)

    $boltPoints = @(
        (New-Object System.Drawing.PointF (S 104), (S 48)),
        (New-Object System.Drawing.PointF (S 67), (S 130)),
        (New-Object System.Drawing.PointF (S 104), (S 123)),
        (New-Object System.Drawing.PointF (S 82), (S 202)),
        (New-Object System.Drawing.PointF (S 174), (S 95)),
        (New-Object System.Drawing.PointF (S 127), (S 103))
    )
    $boltPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $boltPath.AddPolygon($boltPoints)
    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
    $g.TranslateTransform((S 5), (S 7))
    $g.FillPath($shadowBrush, $boltPath)
    $g.ResetTransform()

    $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF((S 58), (S 45), (S 124), (S 160))), ([System.Drawing.Color]::FromArgb(255, 54, 255, 117)), ([System.Drawing.Color]::FromArgb(255, 0, 160, 88)), 70
    $g.FillPath($boltBrush, $boltPath)
    $boltPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210, 174, 255, 205)), (S 5)
    $g.DrawPath($boltPen, $boltPath)

    $moonOuter = New-Object System.Drawing.RectangleF (S 151), (S 49), (S 55), (S 55)
    $moonInner = New-Object System.Drawing.RectangleF (S 134), (S 39), (S 62), (S 62)
    $moonBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 208, 224, 255))
    $cutBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 25, 29, 35))
    $g.FillEllipse($moonBrush, $moonOuter)
    $g.FillEllipse($cutBrush, $moonInner)

    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 63, 255, 147))
    $g.FillEllipse($dotBrush, (S 176), (S 175), (S 17), (S 17))
    $g.FillEllipse($dotBrush, (S 202), (S 175), (S 17), (S 17))
    $g.FillEllipse($dotBrush, (S 150), (S 175), (S 17), (S 17))

    $g.Dispose()
    return $bmp
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Ico {
    param(
        [System.Drawing.Bitmap[]]$Bitmaps,
        [string]$Path
    )

    $pngStreams = @()
    foreach ($bitmap in $Bitmaps) {
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngStreams += ,$stream
    }

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $bw = New-Object System.IO.BinaryWriter $fs
    try {
        $bw.Write([UInt16]0)
        $bw.Write([UInt16]1)
        $bw.Write([UInt16]$Bitmaps.Count)

        $offset = 6 + (16 * $Bitmaps.Count)
        for ($i = 0; $i -lt $Bitmaps.Count; $i++) {
            $bitmap = $Bitmaps[$i]
            $stream = $pngStreams[$i]
            $widthByte = if ($bitmap.Width -ge 256) { 0 } else { [byte]$bitmap.Width }
            $heightByte = if ($bitmap.Height -ge 256) { 0 } else { [byte]$bitmap.Height }
            $bw.Write([byte]$widthByte)
            $bw.Write([byte]$heightByte)
            $bw.Write([byte]0)
            $bw.Write([byte]0)
            $bw.Write([UInt16]1)
            $bw.Write([UInt16]32)
            $bw.Write([UInt32]$stream.Length)
            $bw.Write([UInt32]$offset)
            $offset += [int]$stream.Length
        }

        foreach ($stream in $pngStreams) {
            $bw.Write($stream.ToArray())
        }
    } finally {
        $bw.Dispose()
        foreach ($stream in $pngStreams) { $stream.Dispose() }
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$bitmaps = @($sizes | ForEach-Object { New-IconBitmap -Size $_ })
try {
    Save-Png -Bitmap $bitmaps[-1] -Path $pngPath
    Save-Ico -Bitmaps $bitmaps -Path $icoPath
} finally {
    foreach ($bitmap in $bitmaps) { $bitmap.Dispose() }
}

[pscustomobject]@{
    PngPath = $pngPath
    IcoPath = $icoPath
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$docsDir = Join-Path $repo "docs\images"
$siteDir = Join-Path $repo "site\public"
$logoPath = Join-Path $repo "assets\smart-nap-logo-v2.png"

function C($hex, [int]$alpha = 255) {
    $h = $hex.TrimStart("#")
    return [System.Drawing.Color]::FromArgb(
        $alpha,
        [Convert]::ToInt32($h.Substring(0, 2), 16),
        [Convert]::ToInt32($h.Substring(2, 2), 16),
        [Convert]::ToInt32($h.Substring(4, 2), 16)
    )
}

function FontOf($size, [System.Drawing.FontStyle]$style = [System.Drawing.FontStyle]::Regular) {
    return [System.Drawing.Font]::new("Segoe UI", [single]$size, $style, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-Canvas($w, $h) {
    $bmp = [System.Drawing.Bitmap]::new($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $w, $h),
        (C "07111F"),
        (C "0B1727"),
        12
    )
    $g.FillRectangle($brush, 0, 0, $w, $h)
    $brush.Dispose()
    return @{ Bitmap = $bmp; Graphics = $g }
}

function RoundedPath($x, $y, $w, $h, $r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function FillRound($g, $x, $y, $w, $h, $r, $color) {
    $path = RoundedPath $x $y $w $h $r
    $brush = [System.Drawing.SolidBrush]::new($color)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

function StrokeRound($g, $x, $y, $w, $h, $r, $color, [single]$width = 1) {
    $path = RoundedPath $x $y $w $h $r
    $pen = [System.Drawing.Pen]::new($color, $width)
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function Text($g, $s, $x, $y, $w, $h, $size, $color, [System.Drawing.FontStyle]$style = [System.Drawing.FontStyle]::Regular, $align = "Near") {
    $font = FontOf $size $style
    $brush = [System.Drawing.SolidBrush]::new($color)
    $fmt = [System.Drawing.StringFormat]::new()
    $fmt.Trimming = [System.Drawing.StringTrimming]::None
    $fmt.FormatFlags = [System.Drawing.StringFormatFlags]::NoClip
    if ($align -eq "Center") { $fmt.Alignment = [System.Drawing.StringAlignment]::Center }
    elseif ($align -eq "Far") { $fmt.Alignment = [System.Drawing.StringAlignment]::Far }
    else { $fmt.Alignment = [System.Drawing.StringAlignment]::Near }
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Near
    $g.DrawString($s, $font, $brush, [System.Drawing.RectangleF]::new($x, $y, $w, $h), $fmt)
    $fmt.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Pill($g, $x, $y, $w, $label, $tone) {
    $fg = C "DCEBFF"
    $border = C "29496D"
    $fill = C "0F2136"
    if ($tone -eq "green") { $fg = C "25E690"; $border = C "13885F"; $fill = C "0C3128" }
    if ($tone -eq "amber") { $fg = C "FFAA2A"; $border = C "9C6515"; $fill = C "342714" }
    if ($tone -eq "blue") { $fg = C "7EB7FF"; $border = C "3169A6"; $fill = C "10284A" }
    if ($tone -eq "violet") { $fg = C "B49BFF"; $border = C "5D46AD"; $fill = C "221C43" }
    FillRound $g $x $y $w 34 9 $fill
    StrokeRound $g $x $y $w 34 9 $border 1
    Text $g $label ($x + 14) ($y + 8) ($w - 28) 18 13 $fg ([System.Drawing.FontStyle]::Bold)
}

function DrawLogo($g, $x, $y, $size) {
    if (Test-Path $logoPath) {
        $img = [System.Drawing.Image]::FromFile($logoPath)
        $g.DrawImage($img, $x, $y, $size, $size)
        $img.Dispose()
        return
    }
    FillRound $g $x $y $size $size 14 (C "081422")
    StrokeRound $g $x $y $size $size 14 (C "FFAA2A") 2
    Text $g "S" ($x + 18) ($y + 11) ($size - 36) ($size - 20) 42 (C "FFAA2A") ([System.Drawing.FontStyle]::Bold) "Center"
}

function BackgroundDetails($g, $w, $h) {
    $gridPen = [System.Drawing.Pen]::new((C "203855" 80), 1)
    for ($x = 40; $x -lt $w; $x += 42) { $g.DrawLine($gridPen, $x, 0, $x, $h) }
    for ($y = 40; $y -lt $h; $y += 42) { $g.DrawLine($gridPen, 0, $y, $w, $y) }
    $gridPen.Dispose()

    $penAmber = [System.Drawing.Pen]::new((C "FFAA2A" 160), 2)
    $penGreen = [System.Drawing.Pen]::new((C "25E690" 130), 2)
    $g.DrawLine($penAmber, 0, $h - 130, $w, $h - 225)
    $g.DrawLine($penGreen, $w - 520, 90, $w - 40, 40)
    $penAmber.Dispose()
    $penGreen.Dispose()

    $b1 = [System.Drawing.SolidBrush]::new((C "FFAA2A" 58))
    $b2 = [System.Drawing.SolidBrush]::new((C "25E690" 42))
    $b3 = [System.Drawing.SolidBrush]::new((C "4EA2FF" 48))
    $g.FillEllipse($b1, -95, $h - 215, 310, 310)
    $g.FillEllipse($b2, $w - 220, $h - 360, 330, 330)
    $g.FillEllipse($b3, [int]($w * 0.37), -150, 270, 270)
    $b1.Dispose(); $b2.Dispose(); $b3.Dispose()
}

function Ring($g, $cx, $cy, $r, $value, $label) {
    $rect = [System.Drawing.Rectangle]::new($cx - $r, $cy - $r, $r * 2, $r * 2)
    $base = [System.Drawing.Pen]::new((C "223753"), 16)
    $base.StartCap = "Round"; $base.EndCap = "Round"
    $g.DrawArc($base, $rect, 0, 360)
    $p1 = [System.Drawing.Pen]::new((C "4EA2FF"), 16)
    $p2 = [System.Drawing.Pen]::new((C "25E690"), 16)
    $p1.StartCap = "Round"; $p1.EndCap = "Round"; $p2.StartCap = "Round"; $p2.EndCap = "Round"
    $g.DrawArc($p1, $rect, -90, [int](260 * $value))
    $g.DrawArc($p2, $rect, [int](-90 + (260 * $value)), 75)
    $base.Dispose(); $p1.Dispose(); $p2.Dispose()
    Text $g $label ($cx - $r) ($cy - 22) ($r * 2) 30 31 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold) "Center"
    Text $g "sample apps" ($cx - $r) ($cy + 12) ($r * 2) 20 12 (C "A9BCD4") ([System.Drawing.FontStyle]::Regular) "Center"
}

function SaveImage($canvas, $path) {
    $canvas.Bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Graphics.Dispose()
    $canvas.Bitmap.Dispose()
}

function Render-SocialPreview {
    $c = New-Canvas 1200 630
    $g = $c.Graphics
    BackgroundDetails $g 1200 630

    DrawLogo $g 72 64 70
    Text $g "SMART NAP" 158 78 260 28 24 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "BACKGROUND CONTROL" 160 109 280 22 12 (C "9FB7D8") ([System.Drawing.FontStyle]::Bold)

    Text $g "Smart Background Nap" 72 190 680 72 52 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "Keep apps open. Quiet the background." 76 267 670 38 29 (C "D9E6F7") ([System.Drawing.FontStyle]::Bold)
    Text $g "Local-first Windows optimizer for gaming and multitasking. It reduces safe background CPU, RAM, I/O and EcoQoS pressure while restoring foreground responsiveness fast." 78 330 640 72 21 (C "B8C8DD")

    Pill $g 78 438 145 "No telemetry" "green"
    Pill $g 238 438 140 "No app kill" "amber"
    Pill $g 393 438 125 "Single EXE" "blue"
    Pill $g 533 438 110 "Fast wake" "violet"

    FillRound $g 736 132 390 368 22 (C "0C1829")
    StrokeRound $g 736 132 390 368 22 (C "4E6E98") 1
    Text $g "Nap Engine" 768 180 250 44 34 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Pill $g 975 166 105 "ACTIVE" "green"
    Ring $g 823 320 54 0.72 "24"

    $labels = @(
        @("WAKE", "Fast", "blue"),
        @("MEMORY", "Normal", "green"),
        @("POLICY", "Adaptive", "amber")
    )
    $y = 252
    foreach ($row in $labels) {
        FillRound $g 895 $y 170 43 10 (C "0F2136")
        StrokeRound $g 895 $y 170 43 10 (C "35577F") 1
        Text $g $row[0] 915 ($y + 13) 70 18 12 (C "8099B8") ([System.Drawing.FontStyle]::Bold)
        $col = if ($row[2] -eq "green") { C "25E690" } elseif ($row[2] -eq "amber") { C "FFAA2A" } else { C "7EB7FF" }
        Text $g $row[1] 970 ($y + 10) 80 22 18 $col ([System.Drawing.FontStyle]::Bold) "Far"
        $y += 60
    }
    Text $g "Fictional sample telemetry for documentation." 78 552 520 24 17 (C "9FB7D8")
    SaveImage $c (Join-Path $docsDir "smart-nap-social-preview.png")
}

function Render-AboutPanel {
    $c = New-Canvas 1200 520
    $g = $c.Graphics
    BackgroundDetails $g 1200 520
    FillRound $g 44 42 1112 436 22 (C "0A1423" 235)
    StrokeRound $g 44 42 1112 436 22 (C "2F4E73") 1

    DrawLogo $g 484 72 68
    Text $g "Smart Background Nap" 565 86 420 42 34 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "Local-first Windows optimizer for background pressure control." 210 156 780 34 24 (C "DCEBFF") ([System.Drawing.FontStyle]::Bold) "Center"
    Text $g "Keeps apps open, reduces safe background contention, and restores foreground responsiveness fast. No telemetry. No app killing. No service or driver required." 170 198 860 58 20 (C "B8C8DD") ([System.Drawing.FontStyle]::Regular) "Center"

    $items = @(
        @("Local-first", "green", 140),
        @("Single EXE", "amber", 132),
        @("No telemetry", "blue", 154),
        @("Process-level", "violet", 158),
        @("Fast wake", "blue", 126),
        @("Behavior engine", "green", 180)
    )
    $x = 210
    foreach ($it in $items) {
        Pill $g $x 295 ([int]$it[2]) $it[0] $it[1]
        $x += [int]$it[2] + 16
    }

    $cards = @(
        @("Foreground restore", "Restores process priority as soon as an app comes back."),
        @("Adaptive memory", "Uses cooldowns and pressure bands before trimming."),
        @("Transparent state", "Shows what was changed and why inside the dashboard.")
    )
    $x = 138
    foreach ($card in $cards) {
        FillRound $g $x 368 285 72 14 (C "0F2136")
        StrokeRound $g $x 368 285 72 14 (C "29496D") 1
        Text $g $card[0] ($x + 18) 382 245 22 17 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
        Text $g $card[1] ($x + 18) 409 245 24 13 (C "A9BCD4")
        $x += 310
    }
    SaveImage $c (Join-Path $docsDir "smart-nap-about-panel.png")
}

function Render-EngineStory {
    $c = New-Canvas 1200 520
    $g = $c.Graphics
    BackgroundDetails $g 1200 520
    Text $g "How Smart Nap Works" 70 62 520 52 42 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "The engine uses guarded, process-level decisions. Apps stay open; background pressure gets quieter." 72 118 760 36 20 (C "B8C8DD")

    $steps = @(
        @("1", "Detect context", "Foreground app, memory pressure, CPU bursts, media, downloads and game state."),
        @("2", "Apply safe nap", "Priority, memory priority, I/O priority, EcoQoS and trim cooldowns are selected per app."),
        @("3", "Wake instantly", "When an app returns, priority and responsiveness are restored quickly.")
    )
    $x = 70
    foreach ($s in $steps) {
        FillRound $g $x 188 330 210 18 (C "0C1829")
        StrokeRound $g $x 188 330 210 18 (C "365981") 1
        FillRound $g ($x + 24) 214 52 52 14 (C "10284A")
        Text $g $s[0] ($x + 24) 222 52 34 25 (C "7EB7FF") ([System.Drawing.FontStyle]::Bold) "Center"
        Text $g $s[1] ($x + 94) 216 205 30 25 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
        Text $g $s[2] ($x + 28) 292 270 66 17 (C "B8C8DD")
        $x += 380
    }

    Pill $g 72 432 162 "No telemetry" "green"
    Pill $g 250 432 168 "No app killing" "amber"
    Pill $g 434 432 186 "Permission guard" "violet"
    Pill $g 636 432 160 "Fast wake" "blue"
    SaveImage $c (Join-Path $docsDir "smart-nap-engine-story.png")
}

function Render-Intelligence {
    $c = New-Canvas 1200 520
    $g = $c.Graphics
    BackgroundDetails $g 1200 520
    Text $g "Optional Intelligence Layer" 70 58 560 52 42 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "Smart Learning adapts nap strength from local behavior only. Permission Guard asks for elevation only when an app denies changes." 72 116 760 44 20 (C "B8C8DD")

    FillRound $g 70 188 500 248 18 (C "0C1829")
    StrokeRound $g 70 188 500 248 18 (C "4F6F9B") 1
    Text $g "Smart Learning" 102 220 300 34 30 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "Local profiles" 104 274 160 20 15 (C "8099B8") ([System.Drawing.FontStyle]::Bold)
    Text $g "Adaptive tiers" 294 274 160 20 15 (C "8099B8") ([System.Drawing.FontStyle]::Bold)
    Text $g "Fast wake history" 104 342 190 20 15 (C "8099B8") ([System.Drawing.FontStyle]::Bold)
    Text $g "Memory pressure" 294 342 190 20 15 (C "8099B8") ([System.Drawing.FontStyle]::Bold)
    Pill $g 104 302 150 "Local only" "green"
    Pill $g 294 302 168 "Light / Balanced" "blue"
    Pill $g 104 370 178 "Switch-aware" "violet"
    Pill $g 294 370 150 "Cooldowns" "amber"

    FillRound $g 630 188 500 248 18 (C "0C1829")
    StrokeRound $g 630 188 500 248 18 (C "4F6F9B") 1
    Text $g "Permission Guard" 662 220 340 34 30 (C "FFFFFF") ([System.Drawing.FontStyle]::Bold)
    Text $g "Some apps reject process-level changes without elevation. Smart Nap reports them clearly and offers a single UAC pass when you choose it." 664 278 390 68 19 (C "B8C8DD")
    Pill $g 664 374 152 "Visible denials" "amber"
    Pill $g 832 374 126 "One UAC pass" "blue"
    Pill $g 974 374 112 "No service" "green"
    SaveImage $c (Join-Path $docsDir "smart-nap-intelligence.png")
}

Render-SocialPreview
Render-AboutPanel
Render-EngineStory
Render-Intelligence

if (Test-Path $siteDir) {
    Copy-Item (Join-Path $docsDir "smart-nap-social-preview.png") (Join-Path $siteDir "smart-nap-social-preview.png") -Force
    Copy-Item (Join-Path $docsDir "smart-nap-about-panel.png") (Join-Path $siteDir "smart-nap-about-panel.png") -Force
}

Write-Host "Rendered documentation images."


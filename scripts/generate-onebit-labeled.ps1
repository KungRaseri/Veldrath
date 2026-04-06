# generate-onebit-labeled.ps1  — creates onebit_labeled.png with tile index labels
Add-Type -AssemblyName System.Drawing

$src  = [System.Drawing.Image]::FromFile('c:\code\RealmEngine\RealmUnbound.Assets\GameAssets\tilemaps\sheets\onebit_packed.png')
$bmp  = [System.Drawing.Bitmap]$src

$cols     = 49
$rows     = 22
$tileSize = 16
$spacing  = 0    # packed = no gap
$cellW    = $tileSize + $spacing

# layout: tile (16px) + 1px border gap + 18px label row = 35px per slot
$slotW  = $tileSize + 1
$slotH  = $tileSize + 1 + 18
$outW   = $cols * $slotW + 1
$outH   = $rows * $slotH + 1

$out   = New-Object System.Drawing.Bitmap($outW, $outH)
$g     = [System.Drawing.Graphics]::FromImage($out)
$g.Clear([System.Drawing.Color]::White)

$font  = New-Object System.Drawing.Font('Arial', 6, [System.Drawing.FontStyle]::Regular)
$brush = [System.Drawing.Brushes]::Black
$red   = New-Object System.Drawing.Pen([System.Drawing.Color]::Red, 1)

for ($r = 0; $r -lt $rows; $r++) {
    for ($c = 0; $c -lt $cols; $c++) {
        $idx  = $r * $cols + $c
        $srcX = $c * $cellW
        $srcY = $r * $cellW
        $dstX = $c * $slotW
        $dstY = $r * $slotH

        $srcRect = New-Object System.Drawing.Rectangle($srcX, $srcY, $tileSize, $tileSize)
        $dstRect = New-Object System.Drawing.Rectangle($dstX, $dstY, $tileSize, $tileSize)
        $g.DrawImage($bmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawRectangle($red, $dstX, $dstY, $tileSize, $tileSize)
        $g.DrawString("$idx", $font, $brush, [float]$dstX, [float]($dstY + $tileSize + 2))
    }
}

$outPath = 'c:\code\RealmEngine\scripts\onebit_labeled.png'
$out.Save($outPath)
$g.Dispose()
$out.Dispose()
$bmp.Dispose()
$src.Dispose()
Write-Host "Saved: $outPath  (${outW}x${outH})"

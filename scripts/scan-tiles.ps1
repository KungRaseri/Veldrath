# scan-tiles.ps1
# Pixel-samples tiles from onebit_packed.png.
# Formula: index = row * 49 + col.  Tile size 16px, 0px spacing.
# Samples 5 points per tile: T(col*16+8, row*16+2), B(+8,+14), L(+2,+8), R(+14,+8), M(+8,+8)
#
# Usage:
#   powershell -File scan-tiles.ps1 [-RowMin 0] [-RowMax 21] [-ColMin 0] [-ColMax 48]
#   powershell -File scan-tiles.ps1 -RowMin 4 -RowMax 8 -ColMin 0 -ColMax 10

param([int]$RowMin=0,[int]$RowMax=21,[int]$ColMin=0,[int]$ColMax=48)

Add-Type -AssemblyName System.Drawing
$sheet = [System.Drawing.Bitmap]::FromFile("$PSScriptRoot\..\RealmUnbound.Assets\GameAssets\tilemaps\sheets\onebit_packed.png")

function px($bmp,[int]$x,[int]$y){
    $c = $bmp.GetPixel($x,$y)
    return "R$($c.R)G$($c.G)B$($c.B)"
}

for ($rr = $RowMin; $rr -le $RowMax; $rr++) {
    for ($cc = $ColMin; $cc -le $ColMax; $cc++) {
        $idx = $rr * 49 + $cc
        $bx  = $cc * 16
        $by  = $rr * 16
        $T = px $sheet ($bx+8)  ($by+2)
        $B = px $sheet ($bx+8)  ($by+14)
        $L = px $sheet ($bx+2)  ($by+8)
        $R = px $sheet ($bx+14) ($by+8)
        $M = px $sheet ($bx+8)  ($by+8)
        Write-Host "[$idx] r$rr c$cc  T=$T B=$B L=$L R=$R M=$M"
    }
}

$sheet.Dispose()

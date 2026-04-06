Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile("$PSScriptRoot\..\RealmUnbound.Assets\GameAssets\tilemaps\sheets\onebit_packed.png")

function px($x,$y) {
    $c = $bmp.GetPixel($x,$y)
    return "A=$($c.A) R=$($c.R) G=$($c.G) B=$($c.B)"
}

Write-Host "bg  r0c0 (0,0)      : $(px 8 8)"
Write-Host "202 r4c6 (96+8,64+8): $(px 104 72)"
Write-Host "207 r4c11(176+8,64+8): $(px 184 72)"
Write-Host "445 r9c4 (64+8,144+8): $(px 72 152)"
Write-Host "16  r0c16(256+8, 0+8): $(px 264 8)"
Write-Host "84  r1c35(560+8,16+8): $(px 568 24)"

$bmp.Dispose()

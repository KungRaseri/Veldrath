# deep-scan-flora.ps1  — samples 9 points per tile to better identify tree shapes
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile("$PSScriptRoot\..\RealmUnbound.Assets\GameAssets\tilemaps\sheets\onebit_packed.png")

$green = "R56G217B115"
$bg    = "R71G45B60"

function px($x,$y){
    $c = $bmp.GetPixel($x,$y)
    return "R$($c.R)G$($c.G)B$($c.B)"
}

# score = number of sample points that are green
for ($rr = 0; $rr -le 5; $rr++) {
    for ($cc = 0; $cc -le 48; $cc++) {
        $idx = $rr * 49 + $cc
        $bx  = $cc * 16; $by = $rr * 16
        $pts = @(
            (px ($bx+4)  ($by+4)),
            (px ($bx+8)  ($by+4)),
            (px ($bx+12) ($by+4)),
            (px ($bx+4)  ($by+8)),
            (px ($bx+8)  ($by+8)),
            (px ($bx+12) ($by+8)),
            (px ($bx+4)  ($by+12)),
            (px ($bx+8)  ($by+12)),
            (px ($bx+12) ($by+12))
        )
        $greenCount = ($pts | Where-Object { $_ -eq $green }).Count
        if ($greenCount -ge 3) {
            Write-Host "[$idx] r$rr c$cc  green=$greenCount/9  $(($pts) -join '|')"
        }
    }
}
$bmp.Dispose()

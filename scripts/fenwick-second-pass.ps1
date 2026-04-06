param()
$ErrorActionPreference = "Stop"

$mapPath = "c:\code\RealmEngine\RealmUnbound.Assets\GameAssets\tilemaps\maps\fenwick-crossing.json"
$json    = Get-Content $mapPath -Raw | ConvertFrom-Json
$W       = 30

$objLayer = $json.layers | Where-Object { $_.name -eq "objects" }
$obj      = $objLayer.data
$col      = $json.collisionMask

# ---------------------------------------------------------------------------
# STEP 1 — E-W Crossroads Road at y=11
# x=1  → StraightH 192  (no EndLeft tile in onebit_packed)
# x=2..14  → StraightH 192
# x=15 → FourWay 444 (explicit — old all-zones-first-pass.ps1 was deleted)
# x=16..27 → StraightH 192
# x=28 → EndRight 359
# All road cells: collision=false
# ---------------------------------------------------------------------------
$obj[331] = 192 ; $col[331] = $false  # (1,11) StraightH (no EndLeft in onebit_packed)
foreach ($x in 2..14)  { $i = 11*$W+$x; $obj[$i] = 192; $col[$i] = $false }  # StraightH
$obj[345] = 444 ; $col[345] = $false                                            # (15,11) FourWay
foreach ($x in 16..27) { $i = 11*$W+$x; $obj[$i] = 192; $col[$i] = $false }  # StraightH
$obj[358] = 359 ; $col[358] = $false  # (28,11) EndRight

# ---------------------------------------------------------------------------
# STEP 2 — Bush Garden flanking the fountain (collision=true)
# West hedge x=11, rows 8-10: Bush.DarkGreen (534)
# East hedge x=16, rows 8-10: Bush.Orange (533) top, then DarkGreen (534)
# ---------------------------------------------------------------------------
$obj[251] = 102 ; $col[251] = $true   # (11,8)
$obj[281] = 102 ; $col[281] = $true   # (11,9)
$obj[311] = 102 ; $col[311] = $true   # (11,10)

$obj[256] = 53  ; $col[256] = $true   # (16,8) Bush.Orange
$obj[286] = 102 ; $col[286] = $true   # (16,9) Bush.DarkGreen
$obj[316] = 102 ; $col[316] = $true   # (16,10) Bush.DarkGreen

# ---------------------------------------------------------------------------
# STEP 3 — Quadrant Flora (14 Oak/Pine trees, all collision=true)
# 526 = Oak.LightGreen  527 = Oak.Orange  528 = Oak.DarkGreen  531 = Pine
# ---------------------------------------------------------------------------

# NW quadrant
$obj[62]  = 49  ; $col[62]  = $true   # (2,2)  Oak.LightGreen
$obj[71]  = 53  ; $col[71]  = $true   # (11,2) Oak.Orange
$obj[123] = 55  ; $col[123] = $true   # (3,4)  Oak.DarkGreen
$obj[190] = 53  ; $col[190] = $true   # (10,6) Oak.Orange

# NE quadrant
$obj[78]  = 53  ; $col[78]  = $true   # (18,2) Oak.Orange
$obj[86]  = 55  ; $col[86]  = $true   # (26,2) Oak.DarkGreen

# SW quadrant
$obj[392] = 55  ; $col[392] = $true   # (2,13) Oak.DarkGreen
$obj[399] = 51  ; $col[399] = $true   # (9,13) Pine.DarkGreen
$obj[513] = 53  ; $col[513] = $true   # (3,17) Oak.Orange
$obj[520] = 55  ; $col[520] = $true   # (10,17) Oak.DarkGreen

# SE quadrant
$obj[438] = 55  ; $col[438] = $true   # (18,14) Oak.DarkGreen
$obj[446] = 51  ; $col[446] = $true   # (26,14) Pine.DarkGreen
$obj[588] = 53  ; $col[588] = $true   # (18,19) Oak.Orange
$obj[596] = 55  ; $col[596] = $true   # (26,19) Oak.DarkGreen

# ---------------------------------------------------------------------------
# STEP 4 — Fix collisionMask for all existing interior flora and fountain tiles
# that were incorrectly left as collision=false
# Fountain 3x3 block (12-14, 8-10)
foreach ($i in @(252, 253, 254, 282, 283, 284, 312, 313, 314)) { $col[$i] = $true }
# PineTrees placed in previous passes
foreach ($i in @(98, 142, 155, 174, 265, 472, 485, 504, 535, 548))  { $col[$i] = $true }

# ---------------------------------------------------------------------------
# Persist
# ---------------------------------------------------------------------------
$json | ConvertTo-Json -Depth 10 -Compress | Set-Content $mapPath -Encoding UTF8
Write-Host "fenwick-second-pass: saved."

# ---------------------------------------------------------------------------
# Spot-checks
# ---------------------------------------------------------------------------
$v   = Get-Content $mapPath -Raw | ConvertFrom-Json
$o   = ($v.layers | Where-Object { $_.name -eq "objects" }).data
$c   = $v.collisionMask
$ok  = $true
$W   = 30

function Check($label, $actual, $expected) {
    if ($actual -ne $expected) {
        Write-Host "FAIL $label : got $actual, want $expected"
        $script:ok = $false
    } else {
        Write-Host "OK   $label"
    }
}

# E-W road
Check "(1,11) tile"  $o[331] 192
Check "(1,11) walk"  $c[331] $false
Check "(14,11) tile" $o[11*$W+14] 192
Check "(15,11) tile" $o[345] 444
Check "(15,11) walk" $c[345] $false
Check "(16,11) tile" $o[11*$W+16] 192
Check "(28,11) tile" $o[358] 359
Check "(28,11) walk" $c[358] $false

# Bush garden
Check "(11,8) tile"  $o[251] 102
Check "(11,8) block" $c[251] $true
Check "(16,8) tile"  $o[256] 53
Check "(16,8) block" $c[256] $true
Check "(16,10) tile" $o[316] 102
Check "(16,10) block" $c[316] $true

# Quadrant flora
Check "(2,2) tile"   $o[62]  49
Check "(2,2) block"  $c[62]  $true
Check "(9,13) tile"  $o[399] 51
Check "(9,13) block" $c[399] $true
Check "(18,14) tile" $o[438] 55
Check "(26,19) tile" $o[596] 55

# Collision fixes — fountain
Check "fountain(12,8) block" $c[252] $true
Check "fountain(14,8) block" $c[254] $true
Check "fountain(13,9) block" $c[283] $true
# Collision fixes — existing trees
Check "pine(8,3) block"   $c[98]  $true
Check "pine(5,5) block"   $c[155] $true
Check "pine(25,8) block"  $c[265] $true

if ($ok) { Write-Host "`nAll checks passed." } else { Write-Host "`nSome checks FAILED." ; exit 1 }

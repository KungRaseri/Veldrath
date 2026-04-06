# tilemap-content-pass.ps1
# Populates objects layers for all remaining zone maps + fixes base tiles + dungeon fogMasks.
# Compatible with PowerShell 5.1. All logic is inlined to avoid array-passing edge cases.

$maps = Join-Path $PSScriptRoot "..\RealmUnbound.Assets\GameAssets\tilemaps\maps"

# ─── Tile constants (onebit_packed, formula: row*49+col) ─────────────────────
$TreeE   = 54; $Pine = 51; $TreeB = 50; $TreeD = 53; $BigTree = 102
$Cactus  = 55; $Boulder = 103; $DeadVine = 104
$DirtV   = 343; $DirtH  = 192; $DirtTBR  = 299
# Ground texture tiles (row 0): scatter on objects layer over the dark base
$GrFill  = 7; $GrMed = 6; $GrLight = 5; $GrDead = 1
# Ground scatter lookup tables — deterministic modulo pattern: (col*3 + row*7) % Count
$LutGrass  = @(7, 7, 7, 7, 6, 6, 6, 5, 5, 0)   # 40% dense, 30% med, 20% light, 10% bare
$LutHollow = @(6, 6, 5, 5, 1, 1, 0, 5, 6, 0)    # dark hollow: foliage + dead leaves
$LutSwamp  = @(1, 1, 1, 0, 0, 5, 1, 0, 6, 1)    # swamp: muddy leaves + bare dark

# ─── Helpers ─────────────────────────────────────────────────────────────────
# Load a map JSON
function Load([string]$name) {
    $f = Join-Path $maps $name
    return Get-Content $f -Raw | ConvertFrom-Json
}
# Write a map JSON back (compact)
function Save($j, [string]$name) {
    $f = Join-Path $maps $name
    $j | ConvertTo-Json -Depth 10 -Compress | Set-Content $f -Encoding UTF8
    Write-Host "  OK $name"
}
# Get layer by name
function Layer($j, [string]$n) { return $j.layers | Where-Object { $_.name -eq $n } }
# New objects layer (all -1)
function EmptyObj([int]$sz) {
    $a = New-Object int[] $sz
    for ($i = 0; $i -lt $sz; $i++) { $a[$i] = -1 }
    return $a
}
# New base layer (uniform fill)
function FillArr([int]$sz, [int]$v) {
    $a = New-Object int[] $sz
    for ($i = 0; $i -lt $sz; $i++) { $a[$i] = $v }
    return $a
}

# Fill all cells with a ground-texture scatter using a lookup-table + deterministic modulo.
# Call BEFORE stamping flora/paths so they overwrite the texture at their positions.
# NOTE: $d/$lut must be untyped so PS5.1 passes arrays by reference (same rule as StBorder).
function StGround($d, [int]$w, [int]$h, $lut) {
    $n = $lut.Count
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $d[$y * $w + $x] = $lut[(($x * 3 + $y * 7) % $n)]
        }
    }
}

# Stamp border cells onto flat array $d (top/bottom rows + left/right cols interior)
# $excX, $excY: parallel int arrays of (x,y) exit positions to skip
# NOTE: $d must be untyped so PS5.1 passes it by reference (typed [int[]] params are copied)
function StBorder($d, [int]$w, [int]$h, [int]$tile, [int[]]$excX, [int[]]$excY) {
    $excl = @{}
    for ($ei = 0; $ei -lt $excX.Length; $ei++) { $excl["$($excX[$ei]),$($excY[$ei])"] = $true }
    for ($x = 0; $x -lt $w; $x++) {
        if (-not $excl["$x,0"])         { $d[$x] = $tile }
        if (-not $excl["$x,$($h-1)"])   { $d[($h-1)*$w+$x] = $tile }
    }
    for ($y = 1; $y -lt ($h-1); $y++) {
        if (-not $excl["0,$y"])          { $d[$y*$w] = $tile }
        if (-not $excl["$($w-1),$y"])    { $d[$y*$w+$w-1] = $tile }
    }
}
# Stamp vertical road DirtV onto flat array $d
function StVRoad($d, [int]$w, [int]$x, [int]$y1, [int]$y2) {
    for ($y = $y1; $y -le $y2; $y++) { $d[$y*$w+$x] = 343 }
}
# Stamp horizontal road DirtH onto flat array $d
function StHRoad($d, [int]$w, [int]$y, [int]$x1, [int]$x2) {
    for ($x = $x1; $x -le $x2; $x++) { $d[$y*$w+$x] = 192 }
}

Write-Host "=== Tilemap Content Pass ==="

# ─── Phase 0: Base tile corrections ──────────────────────────────────────────
Write-Host "`n--- Phase 0: Base tile corrections ---"

$j = Load "skarhold.json"   # Stone.M 202 -> Sand.M 445 (coastal, 50x38)
(Layer $j "base").data = FillArr ($j.width * $j.height) 445
Save $j "skarhold.json"

$j = Load "tolvaren.json"   # Sand.M 445 -> Stone.M 202 (volcanic stand-in, 40x30)
(Layer $j "base").data = FillArr ($j.width * $j.height) 202
Save $j "tolvaren.json"

# ─── Phase 2a: fenwick-crossing (patch existing objects) ─────────────────────
Write-Host "`n--- Phase 2a: fenwick-crossing (enhance) ---"
$j = Load "fenwick-crossing.json"; $w = [int]$j.width; $h = [int]$j.height
$d = EmptyObj ($w*$h)
StGround $d $w $h $LutGrass
# Pine scatter (10 positions — collision blocked by fenwick-second-pass.ps1 STEP 4)
$d[3*$w+8]=$Pine;  $d[4*$w+22]=$Pine;  $d[5*$w+5]=$Pine;   $d[5*$w+24]=$Pine
$d[8*$w+25]=$Pine; $d[15*$w+22]=$Pine; $d[16*$w+5]=$Pine;  $d[16*$w+24]=$Pine
$d[17*$w+25]=$Pine; $d[18*$w+8]=$Pine
(Layer $j "objects").data = $d
Save $j "fenwick-crossing.json"

# ─── Phase 2b: aldenmere (30x22, Stone.M, highland town) ─────────────────────
Write-Host "`n--- Phase 2b: aldenmere ---"
$j = Load "aldenmere.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $TreeE @(15,15) @(0,$($h-1))   # exits: (15,0) and (15,h-1)
StVRoad  $d $w 15 1 ($h-2)
$d[5*$w+5]=$TreeE; $d[5*$w+24]=$TreeE; $d[14*$w+8]=$TreeE; $d[14*$w+22]=$TreeE; $d[17*$w+12]=$TreeE
(Layer $j "objects").data = $d; Save $j "aldenmere.json"

# ─── Phase 2c: skarhold objects (50x38, Sand.M, coastal port) ────────────────
Write-Host "`n--- Phase 2c: skarhold (objects) ---"
$j = Load "skarhold.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $TreeB @(25,25) @(0,$($h-1))  # exits: (25,0) and (25,h-1)
StVRoad  $d $w 25 1 ($h-2)
$d[8*$w+10]=$TreeE; $d[8*$w+38]=$TreeE; $d[19*$w+10]=$TreeE
$d[19*$w+38]=$TreeE; $d[28*$w+10]=$TreeE; $d[28*$w+38]=$TreeE
(Layer $j "objects").data = $d; Save $j "skarhold.json"

# ─── Phase 2d: tolvaren objects (40x30, Stone.M, volcanic port) ──────────────
Write-Host "`n--- Phase 2d: tolvaren ---"
$j = Load "tolvaren.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $Cactus @(20,20) @(0,$($h-1))     # exits: (20,0) and (20,h-1)
StVRoad  $d $w 20 1 ($h-2)
$d[5*$w+5]=$Cactus; $d[5*$w+33]=$Cactus; $d[10*$w+8]=$Cactus; $d[10*$w+30]=$Cactus
$d[18*$w+5]=$Cactus; $d[18*$w+33]=$Cactus; $d[23*$w+8]=$Cactus; $d[23*$w+30]=$Cactus
(Layer $j "objects").data = $d; Save $j "tolvaren.json"

# ─── Phase 3a: greenveil-paths (30x22, Grass.M, forest road) ─────────────────
Write-Host "`n--- Phase 3a: greenveil-paths ---"
$j = Load "greenveil-paths.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StGround $d $w $h $LutGrass
StBorder $d $w $h $TreeE @(15,15) @(0,$($h-1))   # exits: (15,0), (15,h-1)
# Forest flanks: cols 1-3 and (w-4)..(w-2), interior rows
for ($fy=1; $fy -lt ($h-1); $fy++) {
    $d[$fy*$w+1]=$TreeE; $d[$fy*$w+2]=$TreeE; $d[$fy*$w+3]=$TreeE
    $d[$fy*$w+($w-4)]=$TreeE; $d[$fy*$w+($w-3)]=$TreeE; $d[$fy*$w+($w-2)]=$TreeE
}
StVRoad $d $w 15 1 ($h-2)   # central road (col 15 not in flanks) - overwrites OK
(Layer $j "objects").data = $d; Save $j "greenveil-paths.json"

# ─── Phase 3b: thornveil-hollow (40x30, Grass.M, deep hollow + dungeon exit) ─
Write-Host "`n--- Phase 3b: thornveil-hollow ---"
$j = Load "thornveil-hollow.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StGround $d $w $h $LutHollow
# Exits: north (20,0), east wall (w-1=39, y=15), south (20,h-1=29)
StBorder $d $w $h $Pine @(20,($w-1),20) @(0,15,($h-1))
StVRoad  $d $w 20 1 14             # N segment y=1..14
$d[15*$w+20] = $DirtTBR             # T-junction at (20,15)
StVRoad  $d $w 20 16 ($h-2)        # S segment y=16..28
StHRoad  $d $w 15 21 ($w-2)        # E spur y=15, x=21..38
(Layer $j "objects").data = $d; Save $j "thornveil-hollow.json"

# ─── Phase 3c: pale-moor (40x30, Stone.M, desolate moorland) ─────────────────
Write-Host "`n--- Phase 3c: pale-moor ---"
$j = Load "pale-moor.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $Boulder @(20,20) @(0,$($h-1))   # rocky border, no road
$d[7*$w+8]=$Boulder; $d[7*$w+30]=$Boulder; $d[13*$w+12]=$DeadVine; $d[13*$w+25]=$DeadVine
$d[20*$w+5]=$Boulder; $d[20*$w+33]=$Boulder; $d[24*$w+15]=$DeadVine; $d[24*$w+22]=$DeadVine
(Layer $j "objects").data = $d; Save $j "pale-moor.json"

# ─── Phase 3d: soddenfen (50x38, Grass.M, swamp + east exit) ────────────────
Write-Host "`n--- Phase 3d: soddenfen ---"
$j = Load "soddenfen.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StGround $d $w $h $LutSwamp
# Exits: north (25,0), east wall (w-1=49,y=19), south (25,h-1=37)
StBorder $d $w $h $BigTree @(25,($w-1),25) @(0,19,($h-1))
StVRoad  $d $w 25 1 18             # N segment y=1..18
$d[19*$w+25] = $DirtTBR             # T-junction at (25,19)
StVRoad  $d $w 25 20 ($h-2)        # S segment y=20..36
StHRoad  $d $w 19 26 ($w-2)        # E spur y=19, x=26..48
$d[8*$w+8]=$BigTree; $d[8*$w+40]=$BigTree; $d[16*$w+12]=$BigTree
$d[16*$w+35]=$BigTree; $d[26*$w+8]=$BigTree; $d[26*$w+40]=$BigTree
(Layer $j "objects").data = $d; Save $j "soddenfen.json"

# ─── Phase 3e: tidewrack-flats (40x30, Sand.M, coastal shore) ────────────────
Write-Host "`n--- Phase 3e: tidewrack-flats ---"
$j = Load "tidewrack-flats.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
for ($y=0; $y -lt $h; $y++) { $d[$y*$w]=$TreeB; $d[$y*$w+$w-1]=$TreeB }  # left+right cols
$d[8*$w+4]=$TreeB; $d[8*$w+34]=$TreeB; $d[15*$w+4]=$TreeB
$d[15*$w+34]=$TreeB; $d[22*$w+4]=$TreeB; $d[22*$w+34]=$TreeB
(Layer $j "objects").data = $d; Save $j "tidewrack-flats.json"

# ─── Phase 3f: saltcliff-heights (40x30, Stone.M, windswept cliffs) ──────────
Write-Host "`n--- Phase 3f: saltcliff-heights ---"
$j = Load "saltcliff-heights.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $Boulder @(20,20) @(0,$($h-1))   # rocky cliff border
StVRoad  $d $w 20 1 ($h-2)
$d[8*$w+6]=$BigTree; $d[8*$w+32]=$BigTree; $d[20*$w+6]=$Boulder; $d[20*$w+32]=$Boulder
(Layer $j "objects").data = $d; Save $j "saltcliff-heights.json"

# ─── Phase 3g: ashfields (50x38, Stone.M, scorched wasteland) ────────────────
Write-Host "`n--- Phase 3g: ashfields ---"
$j = Load "ashfields.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StVRoad $d $w 25 1 ($h-2)   # road only, no perimeter (scorched wasteland)
$d[5*$w+5]=$Cactus;  $d[5*$w+15]=$Cactus;  $d[5*$w+35]=$Cactus;  $d[5*$w+45]=$Cactus
$d[10*$w+8]=$Cactus; $d[10*$w+20]=$Cactus; $d[10*$w+30]=$Cactus; $d[10*$w+42]=$Cactus
$d[18*$w+5]=$Cactus; $d[18*$w+15]=$Cactus; $d[18*$w+35]=$Cactus; $d[18*$w+45]=$Cactus
$d[26*$w+8]=$Cactus; $d[26*$w+20]=$Cactus; $d[26*$w+30]=$Cactus; $d[26*$w+42]=$Cactus
$d[33*$w+5]=$Cactus; $d[33*$w+45]=$Cactus
(Layer $j "objects").data = $d; Save $j "ashfields.json"

# ─── Phase 3h: smoldering-reach (50x38, Stone.M, volcanic approach) ──────────
Write-Host "`n--- Phase 3h: smoldering-reach ---"
$j = Load "smoldering-reach.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StVRoad $d $w 25 1 ($h-2)  # central road (placed first; inner border placed after won't overwrite x=25)
# Inner cactus border at y=1 and y=h-2, cols 1..w-2 except road col 25
for ($x=1; $x -lt ($w-1); $x++) {
    if ($x -ne 25) { $d[1*$w+$x]=$Cactus; $d[($h-2)*$w+$x]=$Cactus }
}
$d[8*$w+5]=$Cactus;  $d[8*$w+20]=$Cactus;  $d[8*$w+30]=$Cactus;  $d[8*$w+45]=$Cactus
$d[15*$w+10]=$Cactus;$d[15*$w+38]=$Cactus
$d[25*$w+5]=$Cactus; $d[25*$w+20]=$Cactus; $d[25*$w+30]=$Cactus; $d[25*$w+45]=$Cactus
$d[30*$w+10]=$Cactus;$d[30*$w+38]=$Cactus
$d[12*$w+12]=$TreeD;$d[12*$w+35]=$TreeD;$d[22*$w+12]=$TreeD;$d[22*$w+35]=$TreeD
(Layer $j "objects").data = $d; Save $j "smoldering-reach.json"

# ─── Phase 4: Dungeon fogMask → all true ─────────────────────────────────────
Write-Host "`n--- Phase 4: Dungeon fogMask fix ---"
foreach ($dungeon in @("verdant-barrow.json","barrow-deeps.json","sunken-name.json","kaldrek-maw.json")) {
    $j = Load $dungeon
    $sz = [int]$j.width * [int]$j.height
    # Use @($true)*N for PS5.1 compatibility: New-Object bool[] serializes as {value:[],Count:N}
    $fog = @($true) * $sz
    $j.fogMask = $fog
    Save $j $dungeon
}

Write-Host "`n=== All done! ==="

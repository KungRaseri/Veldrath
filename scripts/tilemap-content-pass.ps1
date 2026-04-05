# tilemap-content-pass.ps1
# Populates objects layers for all remaining zone maps + fixes base tiles + dungeon fogMasks.
# Compatible with PowerShell 5.1. All logic is inlined to avoid array-passing edge cases.

$maps = Join-Path $PSScriptRoot "..\RealmUnbound.Assets\GameAssets\tilemaps\maps"

# ─── Tile constants (roguelike_base, formula: row*57+col) ────────────────────
$OakDG    = 528; $PineDG = 531; $BushLight = 532; $BushOr = 533; $BushDark = 534
$FruitT   = 536; $DeadT  = 654
$DirtV    = 465; $DirtH  = 408; $DirtTBR   = 461
$WF_TL=229; $WF_TR=228; $WF_ML=230; $WF_M=231; $WF_MR=232; $WF_BL=287; $WF_B=288; $WF_BR=289

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
    for ($y = $y1; $y -le $y2; $y++) { $d[$y*$w+$x] = 465 }
}
# Stamp horizontal road DirtH onto flat array $d
function StHRoad($d, [int]$w, [int]$y, [int]$x1, [int]$x2) {
    for ($x = $x1; $x -le $x2; $x++) { $d[$y*$w+$x] = 408 }
}

Write-Host "=== Tilemap Content Pass ==="

# ─── Phase 0: Base tile corrections ──────────────────────────────────────────
Write-Host "`n--- Phase 0: Base tile corrections ---"

$j = Load "skarhold.json"   # Stone.M 920 -> Sand.M 1262 (coastal, 50x38)
(Layer $j "base").data = FillArr ($j.width * $j.height) 1262
Save $j "skarhold.json"

$j = Load "tolvaren.json"   # Sand.M 1262 -> Stone.M 920 (volcanic stand-in, 40x30)
(Layer $j "base").data = FillArr ($j.width * $j.height) 920
Save $j "tolvaren.json"

# ─── Phase 2a: fenwick-crossing (patch existing objects) ─────────────────────
Write-Host "`n--- Phase 2a: fenwick-crossing (enhance) ---"
$j = Load "fenwick-crossing.json"; $w = [int]$j.width
$d = (Layer $j "objects").data
# PineTree.DarkGreen scatter  [(5,5) already has 531]
$d[5*$w+24] = $PineDG; $d[16*$w+5] = $PineDG; $d[16*$w+24] = $PineDG
# WaterFountain 3x3 at (12-14, 8-10): top-row TL/TR (blank at 13,8), mid ML/M/MR, bot BL/B/BR
$d[8*$w+12]=$WF_TL; $d[8*$w+14]=$WF_TR
$d[9*$w+12]=$WF_ML; $d[9*$w+13]=$WF_M;  $d[9*$w+14]=$WF_MR
$d[10*$w+12]=$WF_BL;$d[10*$w+13]=$WF_B; $d[10*$w+14]=$WF_BR
(Layer $j "objects").data = $d
Save $j "fenwick-crossing.json"

# ─── Phase 2b: aldenmere (30x22, Stone.M, highland town) ─────────────────────
Write-Host "`n--- Phase 2b: aldenmere ---"
$j = Load "aldenmere.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $DeadT @(15,15) @(0,$($h-1))   # exits: (15,0) and (15,h-1)
StVRoad  $d $w 15 1 ($h-2)
$d[5*$w+5]=$DeadT; $d[5*$w+24]=$DeadT; $d[14*$w+8]=$DeadT; $d[14*$w+22]=$DeadT; $d[17*$w+12]=$DeadT
(Layer $j "objects").data = $d; Save $j "aldenmere.json"

# ─── Phase 2c: skarhold objects (50x38, Sand.M, coastal port) ────────────────
Write-Host "`n--- Phase 2c: skarhold (objects) ---"
$j = Load "skarhold.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $BushLight @(25,25) @(0,$($h-1))  # exits: (25,0) and (25,h-1)
StVRoad  $d $w 25 1 ($h-2)
$d[8*$w+10]=$FruitT; $d[8*$w+38]=$FruitT; $d[19*$w+10]=$FruitT
$d[19*$w+38]=$FruitT; $d[28*$w+10]=$FruitT; $d[28*$w+38]=$FruitT
(Layer $j "objects").data = $d; Save $j "skarhold.json"

# ─── Phase 2d: tolvaren objects (40x30, Stone.M, volcanic port) ──────────────
Write-Host "`n--- Phase 2d: tolvaren ---"
$j = Load "tolvaren.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $DeadT @(20,20) @(0,$($h-1))     # exits: (20,0) and (20,h-1)
StVRoad  $d $w 20 1 ($h-2)
$d[5*$w+5]=$DeadT; $d[5*$w+33]=$DeadT; $d[10*$w+8]=$DeadT; $d[10*$w+30]=$DeadT
$d[18*$w+5]=$DeadT; $d[18*$w+33]=$DeadT; $d[23*$w+8]=$DeadT; $d[23*$w+30]=$DeadT
(Layer $j "objects").data = $d; Save $j "tolvaren.json"

# ─── Phase 3a: greenveil-paths (30x22, Grass.M, forest road) ─────────────────
Write-Host "`n--- Phase 3a: greenveil-paths ---"
$j = Load "greenveil-paths.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $OakDG @(15,15) @(0,$($h-1))   # exits: (15,0), (15,h-1)
# Forest flanks: cols 1-3 and (w-4)..(w-2), interior rows
for ($fy=1; $fy -lt ($h-1); $fy++) {
    $d[$fy*$w+1]=$OakDG; $d[$fy*$w+2]=$OakDG; $d[$fy*$w+3]=$OakDG
    $d[$fy*$w+($w-4)]=$OakDG; $d[$fy*$w+($w-3)]=$OakDG; $d[$fy*$w+($w-2)]=$OakDG
}
StVRoad $d $w 15 1 ($h-2)   # central road (col 15 not in flanks) - overwrites OK
(Layer $j "objects").data = $d; Save $j "greenveil-paths.json"

# ─── Phase 3b: thornveil-hollow (40x30, Grass.M, deep hollow + dungeon exit) ─
Write-Host "`n--- Phase 3b: thornveil-hollow ---"
$j = Load "thornveil-hollow.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
# Exits: north (20,0), east wall (w-1=39, y=15), south (20,h-1=29)
StBorder $d $w $h $PineDG @(20,($w-1),20) @(0,15,($h-1))
StVRoad  $d $w 20 1 14             # N segment y=1..14
$d[15*$w+20] = $DirtTBR             # T-junction at (20,15)
StVRoad  $d $w 20 16 ($h-2)        # S segment y=16..28
StHRoad  $d $w 15 21 ($w-2)        # E spur y=15, x=21..38
(Layer $j "objects").data = $d; Save $j "thornveil-hollow.json"

# ─── Phase 3c: pale-moor (40x30, Stone.M, desolate moorland) ─────────────────
Write-Host "`n--- Phase 3c: pale-moor ---"
$j = Load "pale-moor.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $DeadT @(20,20) @(0,$($h-1))   # no road on this moor
$d[7*$w+8]=$DeadT; $d[7*$w+30]=$DeadT; $d[13*$w+12]=$DeadT; $d[13*$w+25]=$DeadT
$d[20*$w+5]=$DeadT; $d[20*$w+33]=$DeadT; $d[24*$w+15]=$DeadT; $d[24*$w+22]=$DeadT
(Layer $j "objects").data = $d; Save $j "pale-moor.json"

# ─── Phase 3d: soddenfen (50x38, Grass.M, swamp + east exit) ────────────────
Write-Host "`n--- Phase 3d: soddenfen ---"
$j = Load "soddenfen.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
# Exits: north (25,0), east wall (w-1=49,y=19), south (25,h-1=37)
StBorder $d $w $h $BushDark @(25,($w-1),25) @(0,19,($h-1))
StVRoad  $d $w 25 1 18             # N segment y=1..18
$d[19*$w+25] = $DirtTBR             # T-junction at (25,19)
StVRoad  $d $w 25 20 ($h-2)        # S segment y=20..36
StHRoad  $d $w 19 26 ($w-2)        # E spur y=19, x=26..48
$d[8*$w+8]=$BushDark; $d[8*$w+40]=$BushDark; $d[16*$w+12]=$BushDark
$d[16*$w+35]=$BushDark; $d[26*$w+8]=$BushDark; $d[26*$w+40]=$BushDark
(Layer $j "objects").data = $d; Save $j "soddenfen.json"

# ─── Phase 3e: tidewrack-flats (40x30, Sand.M, coastal shore) ────────────────
Write-Host "`n--- Phase 3e: tidewrack-flats ---"
$j = Load "tidewrack-flats.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
for ($y=0; $y -lt $h; $y++) { $d[$y*$w]=$BushLight; $d[$y*$w+$w-1]=$BushLight }  # left+right cols
$d[8*$w+4]=$BushLight; $d[8*$w+34]=$BushLight; $d[15*$w+4]=$BushLight
$d[15*$w+34]=$BushLight; $d[22*$w+4]=$BushLight; $d[22*$w+34]=$BushLight
(Layer $j "objects").data = $d; Save $j "tidewrack-flats.json"

# ─── Phase 3f: saltcliff-heights (40x30, Stone.M, windswept cliffs) ──────────
Write-Host "`n--- Phase 3f: saltcliff-heights ---"
$j = Load "saltcliff-heights.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StBorder $d $w $h $DeadT @(20,20) @(0,$($h-1))
StVRoad  $d $w 20 1 ($h-2)
$d[8*$w+6]=$BushOr; $d[8*$w+32]=$BushOr; $d[20*$w+6]=$BushOr; $d[20*$w+32]=$BushOr
(Layer $j "objects").data = $d; Save $j "saltcliff-heights.json"

# ─── Phase 3g: ashfields (50x38, Stone.M, scorched wasteland) ────────────────
Write-Host "`n--- Phase 3g: ashfields ---"
$j = Load "ashfields.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StVRoad $d $w 25 1 ($h-2)   # road only, no perimeter trees (desolate)
$d[5*$w+5]=$DeadT;  $d[5*$w+15]=$DeadT;  $d[5*$w+35]=$DeadT;  $d[5*$w+45]=$DeadT
$d[10*$w+8]=$DeadT; $d[10*$w+20]=$DeadT; $d[10*$w+30]=$DeadT; $d[10*$w+42]=$DeadT
$d[18*$w+5]=$DeadT; $d[18*$w+15]=$DeadT; $d[18*$w+35]=$DeadT; $d[18*$w+45]=$DeadT
$d[26*$w+8]=$DeadT; $d[26*$w+20]=$DeadT; $d[26*$w+30]=$DeadT; $d[26*$w+42]=$DeadT
$d[33*$w+5]=$DeadT; $d[33*$w+45]=$DeadT
(Layer $j "objects").data = $d; Save $j "ashfields.json"

# ─── Phase 3h: smoldering-reach (50x38, Stone.M, volcanic approach) ──────────
Write-Host "`n--- Phase 3h: smoldering-reach ---"
$j = Load "smoldering-reach.json"; $w=[int]$j.width; $h=[int]$j.height
$d = EmptyObj ($w*$h)
StVRoad $d $w 25 1 ($h-2)  # central road (placed first; inner border placed after won't overwrite x=25)
# Inner dead-tree border at y=1 and y=h-2, cols 1..w-2 except road col 25
for ($x=1; $x -lt ($w-1); $x++) {
    if ($x -ne 25) { $d[1*$w+$x]=$DeadT; $d[($h-2)*$w+$x]=$DeadT }
}
$d[8*$w+5]=$DeadT;  $d[8*$w+20]=$DeadT;  $d[8*$w+30]=$DeadT;  $d[8*$w+45]=$DeadT
$d[15*$w+10]=$DeadT;$d[15*$w+38]=$DeadT
$d[25*$w+5]=$DeadT; $d[25*$w+20]=$DeadT; $d[25*$w+30]=$DeadT; $d[25*$w+45]=$DeadT
$d[30*$w+10]=$DeadT;$d[30*$w+38]=$DeadT
$d[12*$w+12]=$BushOr;$d[12*$w+35]=$BushOr;$d[22*$w+12]=$BushOr;$d[22*$w+35]=$BushOr
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

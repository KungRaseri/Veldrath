# generate-maps.ps1
# Generates / overwrites stub tilemap JSON files for all 16 Draveth zones.
#
# ALL zones use the onebit_packed tileset (49×22, 16px, 0px spacing).
# Tile index constants: updated in Phase 3 — see TileIndex.cs for current values.
# See RealmEngine.Shared/Models/TileIndex.cs for the full catalog.
#
# Layer architecture (all maps):
#   Layer 0 "base"    – uniform opaque fill tile,  NEVER -1.
#   Layer 1 "objects" – all -1 (transparent / no objects yet; authored per zone later).
#
# Zone graph (from ApplicationDataSeeder.SeedZoneConnectionsAsync):
#   fenwick-crossing  → greenveil-paths
#   greenveil-paths   ↔ fenwick-crossing, thornveil-hollow
#   thornveil-hollow  → greenveil-paths, verdant-barrow, aldenmere
#   verdant-barrow    → thornveil-hollow
#   aldenmere         ↔ thornveil-hollow, pale-moor
#   pale-moor         ↔ aldenmere, soddenfen
#   soddenfen         ↔ pale-moor, barrow-deeps, tolvaren
#   barrow-deeps      ↔ soddenfen, skarhold (hidden)
#   tolvaren          ↔ soddenfen, tidewrack-flats
#   tidewrack-flats   ↔ tolvaren, saltcliff-heights
#   saltcliff-heights ↔ tidewrack-flats, sunken-name
#   sunken-name       → saltcliff-heights
#   skarhold          ↔ barrow-deeps, ashfields
#   ashfields         ↔ skarhold, smoldering-reach
#   smoldering-reach  ↔ ashfields, kaldrek-maw
#   kaldrek-maw       → smoldering-reach

$mapsDir = "C:\code\RealmEngine\RealmUnbound.Assets\GameAssets\tilemaps\maps"

# Base tile constants (TileIndex.Terrain.*. M)
$Grass  = 265   # TileIndex.Terrain.Grass.M
$Stone  = 202   # TileIndex.Terrain.Stone.M
$Sand   = 445   # TileIndex.Terrain.Sand.M

function e($x, $y, $to) { [pscustomobject]@{ x = $x; y = $y; z = $to } }

# Builds a flat collision mask for a w×h map.
# Border tiles are solid (true), interior is open (false).
# Exit tile positions are explicitly unblocked even if on the border.
function Get-CollisionMask {
    param([object[]]$exits, [int]$w = 40, [int]$h = 30)
    $mask = New-Object bool[] ($w * $h)
    for ($x = 0; $x -lt $w; $x++) {
        $mask[0           * $w + $x] = $true   # top row
        $mask[($h - 1)    * $w + $x] = $true   # bottom row
    }
    for ($y = 1; $y -lt ($h - 1); $y++) {
        $mask[$y * $w + 0]          = $true    # left column
        $mask[$y * $w + ($w - 1)]   = $true    # right column
    }
    # Exit tiles sit on the border — keep them walkable
    foreach ($exit in $exits) {
        $mask[$exit.y * $w + $exit.x] = $false
    }
    return ($mask | ForEach-Object { $_.ToString().ToLower() }) -join ","
}

function Write-Map {
    param(
        [string]  $id,
        [int]     $base,
        [object[]]$exits,
        [int]     $w  = 40,
        [int]     $h  = 30,
        [int]     $sx = -1,
        [int]     $sy = -1
    )

    if ($sx -lt 0) { $sx = [int]($w / 2) }
    if ($sy -lt 0) { $sy = [int]($h / 2) }

    $N    = $w * $h
    $bArr = (([string]$base + ",") * ($N - 1)) + [string]$base
    $oArr = (("-1,") * ($N - 1)) + "-1"
    $cArr = Get-CollisionMask -exits $exits -w $w -h $h
    $fArr = (("false,") * ($N - 1)) + "false"

    $el = ($exits | ForEach-Object {
        "    {`"tileX`": $($_.x), `"tileY`": $($_.y), `"toZoneId`": `"$($_.z)`"}"
    }) -join ",`n"

    $json = @"
{
  "zoneId": "$id",
  "tilesetKey": "onebit_packed",
  "width": $w,
  "height": $h,
  "tileSize": 16,
  "layers": [
    { "name": "base",    "data": [$bArr] },
    { "name": "objects", "data": [$oArr] }
  ],
  "collisionMask": [$cArr],
  "fogMask": [$fArr],
  "exitTiles": [
$el
  ],
  "spawnPoints": [
    { "tileX": $sx, "tileY": $sy }
  ]
}
"@

    [System.IO.File]::WriteAllText("$mapsDir\$id.json", $json, [System.Text.Encoding]::UTF8)
    Write-Host "  Written: $id.json  (${w}x${h})"
}

Write-Host "Generating 16 zone tilemaps (all using onebit_packed)..."

# Zone size categories:
#   Small  (30×22) — starter towns and short paths (fenwick-crossing, greenveil-paths, aldenmere)
#   Medium (40×30) — standard wilderness (default; most zones)
#   Large  (50×38) — epic late-game regions (soddenfen and endgame zones)
#
# Exit tile positions are expressed in the tile-coordinate space of THIS zone's own map.

# -- Grass biome (Terrain.Grass.M = 915) --------------------------------------

# Small starting town — bottom exit to greenveil paths
Write-Map "fenwick-crossing"  $Grass @(e 15 21 "greenveil-paths")                               -w 30 -h 22

# Small path zone — top to fenwick, bottom to thornveil
Write-Map "greenveil-paths"   $Grass @(e 15  0 "fenwick-crossing"; e 15 21 "thornveil-hollow") -w 30 -h 22

# Medium zone — top to greenveil, right to verdant-barrow, bottom to aldenmere
Write-Map "thornveil-hollow"  $Grass `
    @(e 20  0 "greenveil-paths"; e 39 15 "verdant-barrow"; e 20 29 "aldenmere")

# Large marsh zone — top to pale-moor, right to barrow-deeps, bottom to tolvaren
Write-Map "soddenfen"         $Grass `
    @(e 25  0 "pale-moor"; e 49 19 "barrow-deeps"; e 25 37 "tolvaren")                          -w 50 -h 38

# -- Stone biome (Terrain.Stone.M = 920) --------------------------------------

Write-Map "verdant-barrow"    $Stone @(e  0 15 "thornveil-hollow")

# Small hub town — top to thornveil, bottom to pale-moor
Write-Map "aldenmere"         $Stone @(e 15  0 "thornveil-hollow"; e 15 21 "pale-moor")         -w 30 -h 22

Write-Map "pale-moor"         $Stone @(e 20  0 "aldenmere"; e 20 29 "soddenfen")

Write-Map "barrow-deeps"      $Stone @(e  0 15 "soddenfen"; e 20 29 "skarhold")

Write-Map "saltcliff-heights" $Stone @(e 20  0 "tidewrack-flats"; e 20 29 "sunken-name")

Write-Map "sunken-name"       $Stone @(e 20  0 "saltcliff-heights")

Write-Map "skarhold"          $Stone `
    @(e 25  0 "barrow-deeps"; e 25 37 "ashfields")                                              -w 50 -h 38

Write-Map "ashfields"         $Stone `
    @(e 25  0 "skarhold"; e 25 37 "smoldering-reach")                                           -w 50 -h 38

Write-Map "smoldering-reach"  $Stone `
    @(e 25  0 "ashfields"; e 25 37 "kaldrek-maw")                                               -w 50 -h 38

Write-Map "kaldrek-maw"       $Stone @(e 25  0 "smoldering-reach")                              -w 50 -h 38

# -- Sand biome (Terrain.Sand.M = 1262) ----------------------------------------
Write-Map "tolvaren"          $Sand  @(e 20  0 "soddenfen"; e 20 29 "tidewrack-flats")

Write-Map "tidewrack-flats"   $Sand  @(e 20  0 "tolvaren"; e 20 29 "saltcliff-heights")

Write-Host ""
Write-Host "Done! All 16 maps written to: $mapsDir"
Get-ChildItem $mapsDir -Filter "*.json" | Select-Object Name, @{n="KB";e={[math]::Round($_.Length/1KB,1)}} | Format-Table

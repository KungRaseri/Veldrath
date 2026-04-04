# generate-maps.ps1
# Generates / overwrites stub tilemap JSON files for all 16 Draveth zones.
#
# ALL zones use the roguelike_base tileset (57×31, 16px, 1px spacing).
# Tile index constants: TileIndex.Terrain.Grass.M = 915, Stone.M = 920, Sand.M = 1262.
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
$N = 1200   # 40 x 30

# Base tile constants (TileIndex.Terrain.*. M)
$Grass  = 915   # TileIndex.Terrain.Grass.M
$Stone  = 920   # TileIndex.Terrain.Stone.M
$Sand   = 1262  # TileIndex.Terrain.Sand.M

function e($x, $y, $to) { [pscustomobject]@{ x = $x; y = $y; z = $to } }

function Write-Map {
    param(
        [string]  $id,
        [int]     $base,
        [object[]]$exits,
        [int]     $sx = 20,
        [int]     $sy = 15
    )

    $bArr = (([string]$base + ",") * ($N - 1)) + [string]$base
    $oArr = (("-1,") * ($N - 1)) + "-1"
    $cArr = (("false,") * ($N - 1)) + "false"
    $fArr = (("false,") * ($N - 1)) + "false"

    $el = ($exits | ForEach-Object {
        "    {`"tileX`": $($_.x), `"tileY`": $($_.y), `"toZoneId`": `"$($_.z)`"}"
    }) -join ",`n"

    $json = @"
{
  "zoneId": "$id",
  "tilesetKey": "roguelike_base",
  "width": 40,
  "height": 30,
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
    Write-Host "  Written: $id.json"
}

Write-Host "Generating 16 zone tilemaps (all using roguelike_base)..."

# -- Grass biome (Terrain.Grass.M = 915) --------------------------------------
Write-Map "fenwick-crossing"  $Grass @(e 20 29 "greenveil-paths") 18 15

Write-Map "greenveil-paths"   $Grass `
    @(e 20  0 "fenwick-crossing"; e 20 29 "thornveil-hollow")

Write-Map "thornveil-hollow"  $Grass `
    @(e 20  0 "greenveil-paths"; e 39 15 "verdant-barrow"; e 20 29 "aldenmere")

Write-Map "soddenfen"         $Grass `
    @(e 20  0 "pale-moor"; e 39 15 "barrow-deeps"; e 20 29 "tolvaren")

# -- Stone biome (Terrain.Stone.M = 920) --------------------------------------
Write-Map "verdant-barrow"    $Stone @(e  0 15 "thornveil-hollow")

Write-Map "aldenmere"         $Stone `
    @(e 20  0 "thornveil-hollow"; e 20 29 "pale-moor")

Write-Map "pale-moor"         $Stone `
    @(e 20  0 "aldenmere"; e 20 29 "soddenfen")

Write-Map "barrow-deeps"      $Stone `
    @(e  0 15 "soddenfen"; e 20 29 "skarhold")

Write-Map "saltcliff-heights" $Stone `
    @(e 20  0 "tidewrack-flats"; e 20 29 "sunken-name")

Write-Map "sunken-name"       $Stone @(e 20  0 "saltcliff-heights")

Write-Map "skarhold"          $Stone `
    @(e 20  0 "barrow-deeps"; e 20 29 "ashfields")

Write-Map "ashfields"         $Stone `
    @(e 20  0 "skarhold"; e 20 29 "smoldering-reach")

Write-Map "smoldering-reach"  $Stone `
    @(e 20  0 "ashfields"; e 20 29 "kaldrek-maw")

Write-Map "kaldrek-maw"       $Stone @(e 20  0 "smoldering-reach")

# -- Sand biome (Terrain.Sand.M = 1262) ----------------------------------------
Write-Map "tolvaren"          $Sand `
    @(e 20  0 "soddenfen"; e 20 29 "tidewrack-flats")

Write-Map "tidewrack-flats"   $Sand `
    @(e 20  0 "tolvaren"; e 20 29 "saltcliff-heights")

Write-Host ""
Write-Host "Done! All 16 maps written to: $mapsDir"
Get-ChildItem $mapsDir -Filter "*.json" | Select-Object Name, @{n="KB";e={[math]::Round($_.Length/1KB,1)}} | Format-Table

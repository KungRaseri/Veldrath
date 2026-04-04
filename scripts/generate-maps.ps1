# generate-maps.ps1
# Generates / overwrites stub tilemap JSON files for all 16 Draveth zones.
#
# Tile index choices (see TilemapAssets.cs for sheet specs):
#   tiny_town    (12×11, 1px spacing): base = 13  row 1 col 1  stone cobblestone path
#   roguelike_base (57×31, 1px spacing): base = 3   row 0 col 3  flat terrain fill
#   tiny_dungeon (12×11, 1px spacing): base = 1   row 0 col 1  dark stone floor
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

function e($x, $y, $to) { [pscustomobject]@{ x = $x; y = $y; z = $to } }

function Write-Map {
    param(
        [string]  $id,
        [string]  $key,
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
  "tilesetKey": "$key",
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

$TT = 13   # tiny_town
$RB = 3    # roguelike_base
$TD = 1    # tiny_dungeon

Write-Host "Generating 16 zone tilemaps..."

# -- Towns (tiny_town) --------------------------------------------------------
Write-Map "fenwick-crossing"  "tiny_town"      $TT @(e 20 29 "greenveil-paths") 18 15

Write-Map "aldenmere"         "tiny_town"      $TT `
    @(e 20  0 "thornveil-hollow"; e 20 29 "pale-moor")

Write-Map "tolvaren"          "tiny_town"      $TT `
    @(e 20  0 "soddenfen"; e 20 29 "tidewrack-flats")

# Skarhold is Zone.Type = Town but uses tiny_dungeon (Kaldrek forge-city, dark stone)
Write-Map "skarhold"          "tiny_dungeon"   $TD `
    @(e 20  0 "barrow-deeps"; e 20 29 "ashfields")

# -- Wilderness (roguelike_base) ----------------------------------------------
Write-Map "greenveil-paths"   "roguelike_base" $RB `
    @(e 20  0 "fenwick-crossing"; e 20 29 "thornveil-hollow")

Write-Map "thornveil-hollow"  "roguelike_base" $RB `
    @(e 20  0 "greenveil-paths"; e 39 15 "verdant-barrow"; e 20 29 "aldenmere")

Write-Map "pale-moor"         "roguelike_base" $RB `
    @(e 20  0 "aldenmere"; e 20 29 "soddenfen")

Write-Map "soddenfen"         "roguelike_base" $RB `
    @(e 20  0 "pale-moor"; e 39 15 "barrow-deeps"; e 20 29 "tolvaren")

Write-Map "tidewrack-flats"   "roguelike_base" $RB `
    @(e 20  0 "tolvaren"; e 20 29 "saltcliff-heights")

Write-Map "saltcliff-heights" "roguelike_base" $RB `
    @(e 20  0 "tidewrack-flats"; e 20 29 "sunken-name")

Write-Map "ashfields"         "roguelike_base" $RB `
    @(e 20  0 "skarhold"; e 20 29 "smoldering-reach")

Write-Map "smoldering-reach"  "roguelike_base" $RB `
    @(e 20  0 "ashfields"; e 20 29 "kaldrek-maw")

# -- Dungeons (tiny_dungeon) --------------------------------------------------
Write-Map "verdant-barrow"    "tiny_dungeon"   $TD @(e  0 15 "thornveil-hollow")

Write-Map "barrow-deeps"      "tiny_dungeon"   $TD `
    @(e  0 15 "soddenfen"; e 20 29 "skarhold")

Write-Map "sunken-name"       "tiny_dungeon"   $TD @(e 20  0 "saltcliff-heights")

Write-Map "kaldrek-maw"       "tiny_dungeon"   $TD @(e 20  0 "smoldering-reach")

Write-Host ""
Write-Host "Done! All 16 maps written to: $mapsDir"
Get-ChildItem $mapsDir -Filter "*.json" | Select-Object Name, @{n="KB";e={[math]::Round($_.Length/1KB,1)}} | Format-Table

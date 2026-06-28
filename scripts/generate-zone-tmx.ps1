# Generate minimal zone TMX tilemap files for 12 higher-level zones
# Output directory: Veldrath.Assets/GameAssets/tilemaps/maps/

$outputDir = "Veldrath.Assets/GameAssets/tilemaps/maps"

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# CSV row generator: repeats $tileGid $width times, comma-separated
function Get-CsvRow {
    param([int]$width, [int]$tileGid)
    $vals = @()
    for ($x = 0; $x -lt $width; $x++) {
        $vals += $tileGid
    }
    return $vals -join ","
}

# Generate full CSV grid
function Get-CsvGrid {
    param([int]$width, [int]$height, [int]$tileGid)
    $rows = @()
    for ($y = 0; $y -lt $height; $y++) {
        $rows += Get-CsvRow -width $width -tileGid $tileGid
    }
    return $rows -join "`n"
}

# Build XML escaped string safely
function New-TmxFile {
    param(
        [string]$name,          # filename without extension
        [string]$displayName,   # human-readable zone display name
        [int]$width,
        [int]$height,
        [int]$tileGid,          # GID for ground layer (1=grass, 67=dirt, 193=stone)
        [array]$labels          # array of hashtables with Name, Slug, X, Y, Type
    )

    $csvGround = Get-CsvGrid -width $width -height $height -tileGid $tileGid
    $csvZeros  = Get-CsvGrid -width $width -height $height -tileGid 0

    # Calculate center and edge positions in pixels
    $cx = [math]::Floor($width / 2) * 16
    $cy = [math]::Floor($height / 2) * 16
    $northX = $cx;  $northY = 0
    $southX = $cx;  $southY = ($height - 1) * 16
    $eastX  = ($width - 1) * 16;  $eastY  = $cy
    $westX  = 0;                  $westY  = $cy

    # Build labels XML
    $labelsXml = ""
    $labelId = 6
    foreach ($lbl in $labels) {
        $labelsXml += @"
   <object id="$labelId" name="$($lbl.Name)" x="$($lbl.X)" y="$($lbl.Y)">
    <properties>
     <property name="zoneSlug" value="$($lbl.Slug)"/>
     <property name="type" value="$($lbl.Type)"/>
    </properties>
    <point/>
   </object>

"@
        $labelId++
    }

    # Build paths XML (simple main road)
    $pathsXml = @"
   <object id="10" name="main-path" x="0" y="0">
    <polyline points="0,$cy $($width*16),$cy"/>
   </object>
   <object id="11" name="cross-path" x="$cx" y="0">
    <polyline points="0,0 0,$($height*16)"/>
   </object>

"@

    # Map nextobjectid based on total objects
    $nextObjId = 10 + $labels.Count + 2  # 4 exits + 1 spawn + labels + 2 paths

    $tmx = @"
<?xml version="1.0" encoding="UTF-8"?>
<map version="1.11" tiledversion="1.12.1" orientation="orthogonal" renderorder="right-down" width="$width" height="$height" tilewidth="16" tileheight="16" infinite="0" nextlayerid="10" nextobjectid="$nextObjId">
 <properties>
  <property name="tilesetKey" value="roguelike_base"/>
  <property name="zoneId" value="$name"/>
 </properties>
 <tileset firstgid="1" source="../sheets/roguelike_base.tsx"/>
 <objectgroup id="5" name="exits">
  <object id="1" name="north" x="$northX" y="$northY" width="16" height="16"/>
  <object id="2" name="south" x="$southX" y="$southY" width="16" height="16"/>
  <object id="3" name="east" x="$eastX" y="$eastY" width="16" height="16"/>
  <object id="4" name="west" x="$westX" y="$westY" width="16" height="16"/>
 </objectgroup>
 <objectgroup id="6" name="spawns">
  <object id="5" name="default-spawn" x="$cx" y="$cy" width="16" height="16"/>
 </objectgroup>
 <objectgroup id="7" name="labels">
$labelsXml </objectgroup>
 <objectgroup id="8" name="paths">
$pathsXml </objectgroup>
 <layer id="1" name="ground" width="$width" height="$height">
  <data encoding="csv">
$csvGround
  </data>
 </layer>
 <layer id="2" name="detail" width="$width" height="$height">
  <data encoding="csv">
$csvZeros
  </data>
 </layer>
 <layer id="3" name="decoration" width="$width" height="$height">
  <data encoding="csv">
$csvZeros
  </data>
 </layer>
 <layer id="4" name="overhead" width="$width" height="$height">
  <data encoding="csv">
$csvZeros
  </data>
 </layer>
</map>
"@

    $filePath = Join-Path $outputDir "$name.tmx"
    Set-Content -Path $filePath -Value $tmx -Encoding UTF8
    Write-Host "Created: $filePath"
}

# ===== Zone Definitions =====

# --- Greymoor Region ---

# 1. aldenmere (Town, L5-L14, 48x40, dirt tiles)
New-TmxFile -name "aldenmere" -displayName "Aldenmere" -width 48 -height 40 -tileGid 67 -labels @(
    @{Name="Ironhollow Keep"; Slug="ironhollow-keep"; X=160; Y=120; Type="Town"},
    @{Name="Aldenmere Marketplace"; Slug="aldenmere-marketplace"; X=560; Y=160; Type="Town"},
    @{Name="The Grey Cup"; Slug="grey-cup"; X=240; Y=480; Type="Town"}
)

# 2. pale-moor (Wilderness, L7-L14, 64x48, grass tiles)
New-TmxFile -name "pale-moor" -displayName "The Pale Moor" -width 64 -height 48 -tileGid 1 -labels @(
    @{Name="Ashveil Highlands"; Slug="ashveil-highlands"; X=480; Y=320; Type="Wilderness"},
    @{Name="The Moorstone Cairns"; Slug="moorstone-cairns"; X=720; Y=560; Type="Location"},
    @{Name="The Shifting Waymark"; Slug="shifting-waymark"; X=320; Y=640; Type="Hidden"}
)

# 3. soddenfen (Wilderness, L9-L14, 48x40, grass tiles)
New-TmxFile -name "soddenfen" -displayName "The Soddenfen" -width 48 -height 40 -tileGid 1 -labels @(
    @{Name="Fenland Crossing"; Slug="fenland-crossing"; X=384; Y=160; Type="Wilderness"},
    @{Name="The Submerged Ruins"; Slug="submerged-ruins"; X=160; Y=480; Type="Hidden"}
)

# 4. barrow-deeps (Dungeon, L11-L14, 40x30, stone tiles)
New-TmxFile -name "barrow-deeps" -displayName "The Barrow Deeps" -width 40 -height 30 -tileGid 193 -labels @(
    @{Name="Deeps Entrance"; Slug="deeps-entrance"; X=160; Y=120; Type="Dungeon Entrance"},
    @{Name="The Ancestor Vault"; Slug="ancestor-vault"; X=480; Y=240; Type="Dungeon"},
    @{Name="The Relic Chamber"; Slug="relic-chamber"; X=320; Y=400; Type="Hidden"}
)

# --- Saltcliff Region ---

# 5. tolvaren (Town, L10-L20, 48x40, dirt tiles)
New-TmxFile -name "tolvaren" -displayName "Tolvaren" -width 48 -height 40 -tileGid 67 -labels @(
    @{Name="Tolvaren Harbour"; Slug="tolvaren-harbour"; X=160; Y=120; Type="Town"},
    @{Name="Cliff Road Market"; Slug="cliff-road-market"; X=560; Y=160; Type="Town"},
    @{Name="The Saltcrow Inn"; Slug="saltcrow-inn"; X=240; Y=480; Type="Town"}
)

# 6. tidewrack-flats (Wilderness, L12-L20, 64x48, grass tiles)
New-TmxFile -name "tidewrack-flats" -displayName "The Tidewrack Flats" -width 64 -height 48 -tileGid 1 -labels @(
    @{Name="Wrack Shore"; Slug="wrack-shore"; X=480; Y=320; Type="Wilderness"},
    @{Name="The Bone Strand"; Slug="bone-strand"; X=720; Y=560; Type="Wilderness"},
    @{Name="The Tidal Grotto"; Slug="tidal-grotto"; X=320; Y=640; Type="Hidden"}
)

# 7. saltcliff-heights (Wilderness, L14-L20, 48x40, grass tiles)
New-TmxFile -name "saltcliff-heights" -displayName "Saltcliff Heights" -width 48 -height 40 -tileGid 1 -labels @(
    @{Name="The Clifftop Ruins"; Slug="clifftop-ruins"; X=384; Y=160; Type="Location"},
    @{Name="Gull-Rider Camp"; Slug="gull-rider-camp"; X=160; Y=480; Type="Wilderness"},
    @{Name="Storm-Watch Peak"; Slug="storm-watch-peak"; X=560; Y=320; Type="Hidden"}
)

# 8. sunken-name (Dungeon, L16-L20, 40x30, stone tiles)
New-TmxFile -name "sunken-name" -displayName "The Sunken Name" -width 40 -height 30 -tileGid 193 -labels @(
    @{Name="The Drowned Threshold"; Slug="drowned-threshold"; X=160; Y=120; Type="Dungeon Entrance"},
    @{Name="The Flooded Throne Room"; Slug="flooded-throne-room"; X=480; Y=240; Type="Dungeon"},
    @{Name="The Tidelocked Vault"; Slug="tidelocked-vault"; X=320; Y=400; Type="Hidden"}
)

# --- Cinderplain Region ---

# 9. skarhold (Town, L18-L30, 48x40, dirt tiles)
New-TmxFile -name "skarhold" -displayName "Skarhold" -width 48 -height 40 -tileGid 67 -labels @(
    @{Name="The Forge Quarter"; Slug="forge-quarter"; X=160; Y=120; Type="Town"},
    @{Name="Caldera Market"; Slug="caldera-market"; X=560; Y=160; Type="Town"},
    @{Name="The Ashbrand Lodge"; Slug="ashbrand-lodge"; X=240; Y=480; Type="Town"}
)

# 10. ashfields (Wilderness, L20-L25, 64x48, grass tiles)
New-TmxFile -name "ashfields" -displayName "The Ashfields" -width 64 -height 48 -tileGid 1 -labels @(
    @{Name="The Obsidian Grove"; Slug="obsidian-grove"; X=480; Y=320; Type="Wilderness"},
    @{Name="The Scorched Battlefield"; Slug="scorched-battlefield"; X=720; Y=560; Type="Location"},
    @{Name="The Ash Shrine"; Slug="ash-shrine"; X=320; Y=640; Type="Hidden"}
)

# 11. smoldering-reach (Wilderness, L23-L26, 48x40, grass tiles)
New-TmxFile -name "smoldering-reach" -displayName "The Smoldering Reach" -width 48 -height 40 -tileGid 1 -labels @(
    @{Name="The Vent Fields"; Slug="vent-fields"; X=384; Y=160; Type="Wilderness"},
    @{Name="The Pyreling Den"; Slug="pyreling-den"; X=160; Y=480; Type="Wilderness"},
    @{Name="The Lava Bridge"; Slug="lava-bridge"; X=560; Y=320; Type="Hidden"}
)

# 12. kaldrek-maw (Dungeon, L26-L30, 40x30, stone tiles)
New-TmxFile -name "kaldrek-maw" -displayName "Kaldrek's Maw" -width 40 -height 30 -tileGid 193 -labels @(
    @{Name="The Maw Descent"; Slug="maw-descent"; X=160; Y=120; Type="Dungeon Entrance"},
    @{Name="The Fire-Ancient's Chamber"; Slug="fire-ancients-chamber"; X=480; Y=240; Type="Dungeon"},
    @{Name="Kaldrek's Heart"; Slug="kaldreks-heart"; X=320; Y=400; Type="Hidden"}
)

Write-Host ""
Write-Host "All 12 zone TMX files generated successfully in '$outputDir'."

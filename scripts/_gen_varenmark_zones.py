#!/usr/bin/env py
"""Generate enhanced Varenmark zone TMX maps with varied terrain, proper exits, and interactables.

Uses regex-based string manipulation on TMX files.
Usage: py scripts/_gen_varenmark_zones.py
"""

import re
import os

MAPS_DIR = os.path.join(os.path.dirname(__file__), "..", "Veldrath.Assets", "GameAssets", "tilemaps", "maps")

# ── Tile IDs (roguelike_base.tsx) ─────────────────────────────────────────────

T_EMPTY = 0
T_WALL = 2
T_GRASS = 7
T_GRASS2 = 50
T_GRASS3 = 64
T_DIRT = 67
T_GRASS_VAR = 579
T_ROAD = 916
T_STONE = 193
T_WATER = 114
T_WATER2 = 115
T_MUD = 63
T_TREE = 200
T_DEAD_TREE = 202
T_CANOPY = 201
T_ROOF = 400
T_BONE = 301
T_RUBBLE = 302
T_CHAIN = 303
T_PIT = 304

# ── Seeded RNG ────────────────────────────────────────────────────────────────

def rseed(seed):
    seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
    return (seed % 1000000) / 1000000.0

def wchoice(items, weights, seed):
    total = sum(weights)
    r = rseed(seed) * total
    cum = 0
    for item, w in zip(items, weights):
        cum += w
        if r < cum:
            return item
    return items[-1]

def rpick(items, seed):
    return items[int(rseed(seed) * len(items)) % len(items)]

# ── Geometry ──────────────────────────────────────────────────────────────────

def parse_pline_pts(pts_str):
    pts = []
    for pt in pts_str.strip().split():
        if "," in pt:
            try:
                x, y = pt.split(",")
                pts.append((int(float(x)), int(float(y))))
            except ValueError:
                pass
    return pts

def seg_dist(px, py, x1, y1, x2, y2):
    dx, dy = x2 - x1, y2 - y1
    if dx == 0 and dy == 0:
        return ((px - x1)**2 + (py - y1)**2)**0.5
    t = max(0, min(1, ((px - x1)*dx + (py - y1)*dy) / (dx*dx + dy*dy)))
    return ((px - (x1 + t*dx))**2 + (py - (y1 + t*dy))**2)**0.5

def on_path(paths, px, py, radius=16):
    for p in paths:
        ox, oy = p["ox"], p["oy"]
        pts = parse_pline_pts(p["pts"])
        verts = [(ox, oy)]
        cx, cy = ox, oy
        for dx, dy in pts:
            cx += dx; cy += dy
            verts.append((cx, cy))
        for i in range(len(verts)-1):
            if seg_dist(px, py, verts[i][0], verts[i][1], verts[i+1][0], verts[i+1][1]) < radius:
                return True
    return False

def near_path(paths, px, py, outer=48, inner=16):
    for p in paths:
        ox, oy = p["ox"], p["oy"]
        pts = parse_pline_pts(p["pts"])
        verts = [(ox, oy)]
        cx, cy = ox, oy
        for dx, dy in pts:
            cx += dx; cy += dy
            verts.append((cx, cy))
        for i in range(len(verts)-1):
            d = seg_dist(px, py, verts[i][0], verts[i][1], verts[i+1][0], verts[i+1][1])
            if inner < d < outer:
                return True
    return False

# ── Layer generators ──────────────────────────────────────────────────────────

def gen_ground(w, h, cfg, paths):
    tiles = cfg["ground_tiles"]
    wts = cfg["ground_weights"]
    rows = []
    for y in range(h):
        vals = []
        for x in range(w):
            if cfg.get("wall_boundary") and (x==0 or x==w-1 or y==0 or y==h-1):
                vals.append(str(cfg.get("wall_tile", T_WALL)))
            elif cfg.get("road_enabled") and on_path(paths, x*16, y*16):
                vals.append(str(T_ROAD))
            else:
                vals.append(str(wchoice(tiles, wts, x*31 + y*37)))
        rows.append(",".join(vals))
    return rows

def gen_detail(w, h, cfg, paths):
    tiles = cfg.get("detail_tiles", [T_MUD])
    wts = cfg.get("detail_weights", [1])
    dens = cfg.get("detail_density", 0.04)
    rows = []
    for y in range(h):
        vals = []
        for x in range(w):
            if cfg.get("wall_boundary") and (x==0 or x==w-1 or y==0 or y==h-1):
                vals.append("0")
            elif cfg.get("road_enabled") and on_path(paths, x*16, y*16):
                vals.append("0")
            elif cfg.get("road_enabled") and near_path(paths, x*16, y*16, 32) and rseed(x*53+y*71+1000) < 0.2:
                vals.append(str(T_MUD))
            elif rseed(x*53+y*71) < dens:
                vals.append(str(wchoice(tiles, wts, x*53+y*71+100)))
            else:
                vals.append("0")
        rows.append(",".join(vals))
    return rows

def gen_deco(w, h, cfg, paths, labels):
    tiles = cfg.get("decoration_tiles", [T_TREE])
    wts = cfg.get("decoration_weights", [1])
    dens = cfg.get("decoration_density", 0.02)
    rows = []
    for y in range(h):
        vals = []
        for x in range(w):
            if cfg.get("wall_boundary") and (x==0 or x==w-1 or y==0 or y==h-1):
                vals.append("0")
                continue
            if on_path(paths, x*16, y*16, 24):
                vals.append("0")
                continue
            skip = False
            for lbl in labels:
                if abs(x*16 - lbl["x_px"]) < 48 and abs(y*16 - lbl["y_px"]) < 48:
                    skip = True; break
            if skip:
                vals.append("0")
            elif rseed(x*97+y*101) < dens:
                vals.append(str(wchoice(tiles, wts, x*97+y*101+200)))
            else:
                vals.append("0")
        rows.append(",".join(vals))
    return rows

def gen_overhead(w, h, cfg, paths, labels, deco_rows):
    otiles = cfg.get("overhead_tiles", [T_CANOPY])
    dens = cfg.get("overhead_density", 0.01)
    rows = []
    for y in range(h):
        vals = []
        for x in range(w):
            if cfg.get("wall_boundary") and (x==0 or x==w-1 or y==0 or y==h-1):
                vals.append("0")
                continue
            if on_path(paths, x*16, y*16, 24):
                vals.append("0")
                continue
            skip = False
            for lbl in labels:
                if abs(x*16 - lbl["x_px"]) < 48 and abs(y*16 - lbl["y_px"]) < 48:
                    skip = True; break
            if skip:
                vals.append("0")
                continue

            dv = int(deco_rows[y].split(",")[x])
            if dv == T_TREE and rseed(x*151+y*163) < 0.7:
                vals.append(str(T_CANOPY))
            elif x>0 and int(deco_rows[y].split(",")[x-1]) == T_TREE and rseed(x*151+y*163+1) < 0.3:
                vals.append(str(T_CANOPY))
            elif y>0 and int(deco_rows[y-1].split(",")[x]) == T_TREE and rseed(x*151+y*163+2) < 0.3:
                vals.append(str(T_CANOPY))
            elif rseed(x*151+y*163+300) < dens:
                vals.append(str(rpick(otiles, x*151+y*163+400)))
            else:
                vals.append("0")
        rows.append(",".join(vals))
    return rows

# ── XML builders ──────────────────────────────────────────────────────────────

def exit_xml(ecfg, eid):
    name = f' name="{ecfg["name"]}"' if "name" in ecfg else ""
    lines = [
        f'   <object id="{eid}" type="exit"{name} x="{ecfg["x_px"]}" y="{ecfg["y_px"]}" width="16" height="16">',
        '    <properties>',
    ]
    if "displayName" in ecfg:
        lines.append(f'     <property name="displayName" value="{ecfg["displayName"]}"/>')
    if "toRegionId" in ecfg:
        lines.append(f'     <property name="toRegionId" value="{ecfg["toRegionId"]}"/>')
    if "toZoneId" in ecfg:
        lines.append(f'     <property name="toZoneId" value="{ecfg["toZoneId"]}"/>')
    lines.append('    </properties>')
    lines.append('   </object>')
    return "\n".join(lines)

def ia_xml(icfg, iid):
    otype = icfg.get("type", "interactable")
    name = icfg["displayName"]
    lines = [
        f'   <object id="{iid}" name="{name}" type="{otype}" x="{icfg["x_px"]}" y="{icfg["y_px"]}" width="16" height="16">',
        '    <properties>',
    ]
    if otype == "npc":
        lines.append(f'     <property name="npcArchetype" value="{icfg.get("npcArchetype", "")}"/>')
    else:
        lines.append(f'     <property name="interactType" value="{icfg.get("interactType", "")}"/>')
    lines.append(f'     <property name="displayName" value="{name}"/>')
    lines.append('    </properties>')
    lines.append('   </object>')
    return "\n".join(lines)

# ── File patching ─────────────────────────────────────────────────────────────

def patch_file(content, zone_id, cfg, w, h):
    """Apply all patches to the file content string."""
    paths_raw = re.findall(
        r'<objectgroup[^>]*name="paths"[^>]*>(.*?)</objectgroup>',
        content, re.DOTALL
    )
    paths = []
    for og in paths_raw:
        for m in re.finditer(r'<object[^>]*?x="([^"]*)"[^>]*?y="([^"]*)"[^>]*?>.*?<polyline points="([^"]*)"', og, re.DOTALL):
            paths.append({"ox": int(float(m.group(1))), "oy": int(float(m.group(2))), "pts": m.group(3)})

    labels_raw = re.findall(
        r'<objectgroup[^>]*name="labels"[^>]*>(.*?)</objectgroup>',
        content, re.DOTALL
    )
    labels = []
    for og in labels_raw:
        for m in re.finditer(r'<object[^>]*?name="([^"]*)"[^>]*?x="([^"]*)"[^>]*?y="([^"]*)"', og):
            labels.append({"name": m.group(1), "x_px": int(float(m.group(2))), "y_px": int(float(m.group(3)))})

    # Generate layers
    g = gen_ground(w, h, cfg, paths)
    d = gen_detail(w, h, cfg, paths)
    c = gen_deco(w, h, cfg, paths, labels)
    o = gen_overhead(w, h, cfg, paths, labels, c)

    layer_map = {"ground": g, "detail": d, "decoration": c, "overhead": o}

    for lname, rows in layer_map.items():
        csv = "\n".join(rows)
        # Match layer open tag + data open tag, then ANY content, then data close + layer close
        pat = rf'(<layer[^>]*name="{lname}"[^>]*>\s*<data encoding="csv">)\s*\n.*?\n(\s*</data>\s*</layer>)'
        repl = rf"\1\n{csv}\n  \2"
        content = re.sub(pat, repl, content, count=1, flags=re.DOTALL)

    # Patch exits
    exits_cfg = cfg.get("exits", [])
    exits_block = "\n".join(exit_xml(ec, i+1) for i, ec in enumerate(exits_cfg))
    # Handle both self-closing and normal objectgroup
    exits_pat = r'(<objectgroup[^>]*name="exits"[^>]*)>.*?(</objectgroup>)'
    exits_repl = rf'\1>\n{exits_block}\n  \2'
    content = re.sub(exits_pat, exits_repl, content, count=1, flags=re.DOTALL)

    # Patch interactables
    ia_cfg = cfg.get("interactables", [])
    ia_block = "\n".join(ia_xml(ic, i+1) for i, ic in enumerate(ia_cfg))
    # Handle self-closing: <objectgroup id="3" name="interactables"/>
    ia_selfclose = r'<objectgroup([^>]*name="interactables"[^>]*)/>'
    if re.search(ia_selfclose, content):
        content = re.sub(ia_selfclose, rf'<objectgroup\1>\n{ia_block}\n  </objectgroup>', content, count=1)
    else:
        ia_pat = r'(<objectgroup[^>]*name="interactables"[^>]*)>.*?(</objectgroup>)'
        ia_repl = rf'\1>\n{ia_block}\n  \2'
        content = re.sub(ia_pat, ia_repl, content, count=1, flags=re.DOTALL)

    # Update nextobjectid
    all_ids = re.findall(r'<object[^>]*\sid="(\d+)"', content)
    max_id = max(int(i) for i in all_ids) if all_ids else 0
    content = re.sub(r'nextobjectid="\d+"', f'nextobjectid="{max_id + 1}"', content, count=1)

    return content

# ── Zone configs ──────────────────────────────────────────────────────────────

ZONES = {
    "the-droveway": {
        "theme": "pastoral",
        "ground_tiles": [T_GRASS, T_GRASS3, T_GRASS_VAR, T_DIRT],
        "ground_weights": [4, 3, 2, 1],
        "detail_tiles": [T_MUD, T_GRASS2], "detail_weights": [1, 3], "detail_density": 0.05,
        "decoration_tiles": [T_TREE, T_DEAD_TREE], "decoration_weights": [3, 1], "decoration_density": 0.02,
        "overhead_tiles": [T_CANOPY], "overhead_density": 0.01,
        "road_enabled": True, "wall_boundary": False,
        "exits": [
            {"name": "to-varenmark", "displayName": "Return to Varenmark", "toRegionId": "varenmark", "x_px": 752, "y_px": 0},
            {"name": "to-crestfall", "displayName": "Road to Crestfall", "toZoneId": "crestfall", "x_px": 752, "y_px": 624},
            {"name": "to-ashlenwood", "displayName": "Path to Ashlen Wood", "toZoneId": "ashlen-wood", "x_px": 0, "y_px": 320},
        ],
        "interactables": [
            {"type": "npc", "displayName": "Droveway Farmer", "npcArchetype": "farmer", "x_px": 640, "y_px": 336},
            {"type": "npc", "displayName": "Traveling Merchant", "npcArchetype": "merchant", "x_px": 1104, "y_px": 192},
            {"type": "interactable", "displayName": "Weathered Waypost", "interactType": "waypost", "x_px": 1104, "y_px": 208},
            {"type": "interactable", "displayName": "Abandoned Cart", "interactType": "salvage", "x_px": 400, "y_px": 400},
            {"type": "interactable", "displayName": "Scarecrow", "interactType": "lore-marker", "x_px": 912, "y_px": 288},
        ],
    },
    "ashlen-wood": {
        "theme": "forest",
        "ground_tiles": [T_GRASS, T_GRASS3, T_GRASS_VAR, T_MUD],
        "ground_weights": [5, 3, 2, 1],
        "detail_tiles": [T_MUD, T_GRASS2], "detail_weights": [1, 4], "detail_density": 0.06,
        "decoration_tiles": [T_TREE, T_DEAD_TREE], "decoration_weights": [5, 1], "decoration_density": 0.05,
        "overhead_tiles": [T_CANOPY], "overhead_density": 0.04,
        "road_enabled": True, "wall_boundary": False,
        "exits": [
            {"name": "to-varenmark", "displayName": "Return to Varenmark", "toRegionId": "varenmark", "x_px": 624, "y_px": 0},
            {"name": "to-crestfall", "displayName": "Road to Crestfall", "toZoneId": "crestfall", "x_px": 624, "y_px": 64},
            {"name": "to-grevenmire", "displayName": "Path to Grevenmire", "toZoneId": "grevenmire", "x_px": 336, "y_px": 704},
            {"name": "to-droveway", "displayName": "Track to The Droveway", "toZoneId": "the-droveway", "x_px": 928, "y_px": 384},
        ],
        "interactables": [
            {"type": "npc", "displayName": "Woodsman", "npcArchetype": "woodsman", "x_px": 480, "y_px": 400},
            {"type": "npc", "displayName": "Forest Hermit", "npcArchetype": "hermit", "x_px": 800, "y_px": 560},
            {"type": "interactable", "displayName": "Ancient Shrine", "interactType": "shrine", "x_px": 336, "y_px": 672},
            {"type": "interactable", "displayName": "Hollow Stump", "interactType": "salvage", "x_px": 640, "y_px": 320},
            {"type": "interactable", "displayName": "Fairy Ring", "interactType": "lore-marker", "x_px": 400, "y_px": 256},
        ],
    },
    "grevenmire": {
        "theme": "swamp",
        "ground_tiles": [T_GRASS2, T_MUD, T_DIRT, T_WATER],
        "ground_weights": [4, 3, 1, 1],
        "detail_tiles": [T_MUD, T_GRASS2], "detail_weights": [2, 1], "detail_density": 0.06,
        "decoration_tiles": [T_DEAD_TREE, T_TREE], "decoration_weights": [3, 1], "decoration_density": 0.03,
        "overhead_tiles": [T_DEAD_TREE, T_CANOPY], "overhead_density": 0.015,
        "road_enabled": True, "wall_boundary": False,
        "exits": [
            {"name": "to-varenmark", "displayName": "Return to Varenmark", "toRegionId": "varenmark", "x_px": 0, "y_px": 432},
            {"name": "to-crestfall", "displayName": "Path to Crestfall", "toZoneId": "crestfall", "x_px": 32, "y_px": 432},
            {"name": "to-ashlenwood", "displayName": "Track to Ashlen Wood", "toZoneId": "ashlen-wood", "x_px": 624, "y_px": 880},
        ],
        "interactables": [
            {"type": "npc", "displayName": "Bog Hermit", "npcArchetype": "hermit", "x_px": 960, "y_px": 272},
            {"type": "npc", "displayName": "Marsh Guide", "npcArchetype": "guide", "x_px": 288, "y_px": 432},
            {"type": "interactable", "displayName": "Sunken Estate Ruins", "interactType": "lore-marker", "x_px": 960, "y_px": 256},
            {"type": "interactable", "displayName": "Bubbling Mud Pool", "interactType": "hazard", "x_px": 480, "y_px": 560},
            {"type": "interactable", "displayName": "Old Ferry Post", "interactType": "waypost", "x_px": 160, "y_px": 432},
        ],
    },
    "the-halrow": {
        "theme": "dungeon",
        "ground_tiles": [T_STONE, T_STONE], "ground_weights": [1, 1],
        "detail_tiles": [T_MUD, T_DIRT], "detail_weights": [1, 1], "detail_density": 0.04,
        "decoration_tiles": [T_BONE, T_RUBBLE, T_DEAD_TREE], "decoration_weights": [2, 3, 1], "decoration_density": 0.03,
        "overhead_tiles": [T_ROOF], "overhead_density": 0.005,
        "road_enabled": False, "wall_boundary": True, "wall_tile": T_WALL,
        "exits": [
            {"name": "to-varenmark", "displayName": "Return to Varenmark", "toRegionId": "varenmark", "x_px": 496, "y_px": 752},
        ],
        "interactables": [
            {"type": "npc", "displayName": "Trapped Miner", "npcArchetype": "miner", "x_px": 496, "y_px": 512},
            {"type": "npc", "displayName": "Cultist Acolyte", "npcArchetype": "cultist", "x_px": 672, "y_px": 320},
            {"type": "interactable", "displayName": "Ancient Sarcophagus", "interactType": "lore-marker", "x_px": 336, "y_px": 336},
            {"type": "interactable", "displayName": "Collapsed Passage", "interactType": "blocked", "x_px": 672, "y_px": 304},
            {"type": "interactable", "displayName": "Dusty Grimoire", "interactType": "salvage", "x_px": 496, "y_px": 336},
        ],
    },
    "drowning-pits": {
        "theme": "pit_dungeon",
        "ground_tiles": [T_STONE, T_STONE, T_WATER], "ground_weights": [5, 4, 1],
        "detail_tiles": [T_MUD, T_DIRT], "detail_weights": [2, 1], "detail_density": 0.03,
        "decoration_tiles": [T_BONE, T_CHAIN, T_RUBBLE], "decoration_weights": [3, 2, 2], "decoration_density": 0.03,
        "overhead_tiles": [T_CHAIN], "overhead_density": 0.005,
        "road_enabled": False, "wall_boundary": True, "wall_tile": T_WALL,
        "exits": [
            {"name": "to-varenmark", "displayName": "Return to Varenmark", "toRegionId": "varenmark", "x_px": 384, "y_px": 16},
        ],
        "interactables": [
            {"type": "npc", "displayName": "Wounded Delver", "npcArchetype": "miner", "x_px": 384, "y_px": 208},
            {"type": "npc", "displayName": "Shade Warden", "npcArchetype": "cultist", "x_px": 528, "y_px": 560},
            {"type": "interactable", "displayName": "Flooded Workings", "interactType": "hazard", "x_px": 240, "y_px": 448},
            {"type": "interactable", "displayName": "Rusted Winch", "interactType": "salvage", "x_px": 384, "y_px": 176},
            {"type": "interactable", "displayName": "Deepest Warning Plaque", "interactType": "lore-marker", "x_px": 528, "y_px": 544},
        ],
    },
}

# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    print("=" * 60)
    print("  Varenmark Zone Map Enhancement Script v3")
    print("=" * 60)

    for zone_id in ["the-droveway", "ashlen-wood", "grevenmire", "the-halrow", "drowning-pits"]:
        cfg = ZONES.get(zone_id)
        if not cfg:
            print(f"  WARNING: No config for {zone_id}")
            continue

        fpath = os.path.join(MAPS_DIR, f"{zone_id}.tmx")
        if not os.path.exists(fpath):
            print(f"  SKIP: {fpath} not found")
            continue

        print(f"\n  Processing: {zone_id}.tmx ({cfg['theme']} theme)")

        with open(fpath, "r", encoding="utf-8") as f:
            content = f.read()

        wm = re.search(r'<map[^>]*\bwidth="(\d+)"', content)
        hm = re.search(r'<map[^>]*\bheight="(\d+)"', content)
        w = int(wm.group(1))
        h = int(hm.group(1))

        content = patch_file(content, zone_id, cfg, w, h)

        with open(fpath, "w", encoding="utf-8") as f:
            f.write(content)

        ec = len(cfg.get("exits", []))
        ic = len(cfg.get("interactables", []))
        print(f"    [OK] Enhanced {zone_id}.tmx ({w}x{h}, {ec} exits, {ic} interactables)")

    print(f"\n{'=' * 60}")
    print("  All 5 Varenmark zone maps enhanced.")
    print(f"{'=' * 60}")

if __name__ == "__main__":
    main()

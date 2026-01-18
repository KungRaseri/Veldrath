#!/usr/bin/env python3
"""
Add descriptions to all items in catalog JSON files.
"""

import json
import os
from pathlib import Path

# Weapon descriptions
WEAPON_DESCRIPTIONS = {
    # Heavy Blades
    "longsword": "A versatile blade forged for balance and precision, favored by warriors who value technique over brute force.",
    "greatsword": "A massive two-handed blade capable of cleaving through armor and bone with devastating force.",
    "katana": "A curved blade of exceptional craftsmanship, designed for swift, lethal strikes with a single edge.",
    "broadsword": "A wide, sturdy blade built for powerful slashing attacks and reliable defense.",
    "claymore": "A massive highland sword wielded with both hands, delivering crushing overhead strikes.",
    "saber": "A curved cavalry sword designed for slashing attacks from horseback or in close combat.",
    
    # Light Blades
    "shortsword": "A compact blade favored for its maneuverability in tight spaces and quick strikes.",
    "scimitar": "A gracefully curved blade hailing from desert lands, deadly in the hands of a skilled warrior.",
    "rapier": "An elegant thrusting sword designed for precision strikes and refined swordsmanship.",
    "gladius": "A short Roman sword built for brutal efficiency in close-quarters combat.",
    "dagger": "A small blade easily concealed, lethal in the hands of an assassin or when thrown.",
    "dirk": "A long dagger with a straight blade, traditional among highland warriors and scouts.",
    "stiletto": "A thin, needle-like blade designed to pierce through gaps in armor.",
    "kris": "A wavy-bladed dagger with mystical significance, said to hold spiritual power.",
    "tanto": "A short blade traditionally paired with a katana, ideal for close combat.",
    "kukri": "A curved blade from mountain regions, equally useful as tool and weapon.",
    "rondel": "A stiff-bladed dagger with a sharp point, designed to puncture armor joints.",
    "main-gauche": "A parrying dagger used in the off-hand for defensive maneuvers.",
    
    # Axes
    "battleaxe": "A heavy chopping weapon that combines raw power with tactical versatility.",
    "handaxe": "A compact axe easily wielded or thrown, useful both in combat and utility.",
    "greataxe": "A massive two-handed axe capable of splitting shields and skulls with a single blow.",
    "waraxe": "A formidable axe designed specifically for warfare, balancing power and control.",
    "hatchet": "A small utility axe that doubles as an effective throwing weapon.",
    "tomahawk": "A light throwing axe used by tribal warriors, deadly at range or in melee.",
    "cleaver": "A heavy chopping blade, originally a butcher's tool, repurposed for brutal combat.",
    "poleaxe": "A pole-mounted axe head combined with other striking surfaces for maximum versatility.",
    
    # Bows
    "longbow": "A tall bow requiring strength to draw, capable of impressive range and penetration.",
    "shortbow": "A compact bow favored by scouts and hunters for its portability and ease of use.",
    "composite-bow": "A sophisticated bow made from multiple materials, offering superior power and accuracy.",
    "recurve-bow": "A bow with curved tips that stores more energy, delivering faster and more powerful shots.",
    "crossbow": "A mechanical bow that trades firing speed for raw power and ease of aim.",
    "heavy-crossbow": "A massive crossbow capable of punching through heavy armor at considerable range.",
    "warbow": "A powerful military bow designed to pierce armor, requiring exceptional strength to draw.",
    "hunting-bow": "A lightweight bow designed for hunting game, reliable and easy to maintain.",
    
    # Polearms
    "spear": "A versatile weapon consisting of a pointed blade on a shaft, effective at keeping enemies at bay.",
    "lance": "A long cavalry weapon designed for devastating charges, unwieldy on foot.",
    "javelin": "A light throwing spear that can also be used in melee, favored by skirmishers.",
    "pike": "An extremely long spear used in formations, presenting a wall of points to cavalry and infantry.",
    "halberd": "A pole weapon combining an axe blade, spike, and hook for maximum versatility.",
    "trident": "A three-pronged spear associated with sea gods, deadly for thrusting attacks.",
    "partisan": "An ornate pole weapon with a broad blade and side projections, favored by guards.",
    "glaive": "A pole weapon with a single-edged blade, combining reach with slashing power.",
    
    # Blunt
    "mace": "A heavy metal club designed to crush armor and bones with brutal efficiency.",
    "warhammer": "A balanced hammer built for war, capable of denting the heaviest armor.",
    "flail": "A hinged weapon with a spiked head on a chain, difficult to defend against.",
    "morning-star": "A spiked mace designed to penetrate armor and cause devastating wounds.",
    "club": "A simple wooden bludgeon, crude but effective in untrained hands.",
    "maul": "A massive two-handed hammer that can shatter shields and pulverize armor.",
    "cudgel": "A thick stick or light club, the weapon of choice for tavern brawlers and thugs.",
    "scepter": "An ornamental rod that can serve as a weapon when magic is not enough.",
    
    # Staves
    "staff": "A wooden pole often used by travelers and mages as both walking aid and weapon.",
    "quarterstaff": "A sturdy wooden staff wielded with both hands, offering reach and defensive capability.",
    "war-staff": "A reinforced staff designed for combat, often capped with metal for extra striking power.",
    "battle-staff": "A heavy combat staff capable of breaking bones and deflecting sword strikes.",
    "rod": "A short metal rod infused with magical energy, serving as both focus and weapon.",
    "scepter-staff": "An ornate staff that channels magical power while serving as a symbol of authority.",
}

# Armor descriptions
ARMOR_DESCRIPTIONS = {
    # Light Armor
    "coif": "A chainmail hood that protects the head and neck without restricting vision or movement.",
    "cowl": "A hooded garment favored by rogues and scouts, offering subtle protection while maintaining stealth.",
    "hood": "A simple cloth hood that provides minimal protection but excellent concealment in shadows.",
    "leather-helmet": "A reinforced leather cap providing basic head protection without the weight of metal.",
    "cap": "A padded cloth cap offering minimal protection, favored by those who value mobility.",
    "bandana": "A simple cloth headband that offers no real protection but identifies its wearer.",
    "mask": "A concealing face covering that protects identity more than flesh.",
    "leather-armor": "Supple armor made from tanned hide, offering protection without sacrificing mobility.",
    "studded-leather": "Leather armor reinforced with metal studs for additional defense.",
    "hide-armor": "Thick furs and hides providing warmth and protection in harsh climates.",
    "padded-armor": "Quilted cloth armor filled with padding to absorb blows.",
    "leather-cuirass": "A fitted leather chest piece that provides solid torso protection.",
    "brigandine": "Small metal plates riveted inside a leather vest, combining flexibility with defense.",
    "leather-gloves": "Simple leather handwear providing grip and basic hand protection.",
    "gauntlets": "Reinforced gloves designed to protect hands while maintaining dexterity.",
    "bracers": "Leather arm guards protecting the forearms from slashes and arrows.",
    "leather-boots": "Sturdy leather footwear designed for long marches and rough terrain.",
    "leather-greaves": "Shin guards providing protection without impeding movement.",
    "leather-pants": "Reinforced leather leggings offering leg protection and durability.",
    "leather-belt": "A sturdy belt for carrying equipment and weapons.",
    
    # Medium Armor
    "chainmail-hood": "An interlocking metal hood providing excellent head protection.",
    "kettle-helm": "A wide-brimmed helmet offering good protection and visibility.",
    "norman-helm": "A conical helmet with a nasal guard, effective against overhead strikes.",
    "barbute": "A helmet that covers the entire head with openings for eyes and mouth.",
    "chain-shirt": "A shirt of interlocking metal rings, the standard armor of soldiers.",
    "scale-mail": "Overlapping scales attached to a backing, providing flexible protection.",
    "ring-mail": "Metal rings sewn onto leather, offering better defense than plain hide.",
    "chainmail": "Full body coverage of interlocking metal rings, heavy but protective.",
    "chain-gloves": "Chainmail gloves protecting hands while allowing weapon grip.",
    "plated-gloves": "Leather gloves with metal plates covering the back of the hand.",
    "chain-boots": "Boots reinforced with chainmail for leg protection.",
    "plated-boots": "Leather boots with metal plates protecting the shins and feet.",
    "chain-pants": "Leg armor made of chainmail links, providing flexible protection.",
    "plated-pants": "Leather pants reinforced with metal plates at vital points.",
    
    # Heavy Armor
    "great-helm": "A fully enclosed helmet offering maximum protection at the cost of visibility.",
    "bascinet": "A close-fitting helmet with a pointed top, designed to deflect blows.",
    "armet": "A sophisticated helmet that completely encases the head with articulated visors.",
    "plate-helmet": "A full plate helmet providing maximum head protection.",
    "plate-mail": "Full armor of shaped metal plates, the pinnacle of protective gear.",
    "full-plate": "Complete suit of articulated plate armor, nearly impervious to common weapons.",
    "splint-mail": "Narrow vertical metal strips riveted to leather backing.",
    "banded-mail": "Horizontal metal bands fastened to leather and chainmail backing.",
    "plate-gloves": "Articulated metal gauntlets protecting the entire hand.",
    "plate-boots": "Fully plated footwear providing maximum protection to feet and lower legs.",
    "plate-greaves": "Full plate leg armor from knee to ankle.",
    "plate-pants": "Articulated leg armor of shaped metal plates.",
    
    # Shields
    "buckler": "A small round shield held in the fist, used for parrying rather than full coverage.",
    "heater-shield": "A medium triangular shield offering good protection without excessive weight.",
    "kite-shield": "A large teardrop-shaped shield providing excellent coverage, especially for cavalry.",
    "tower-shield": "A massive rectangular shield offering near-total body coverage.",
    "round-shield": "A circular shield favored by warriors across many cultures.",
}

def add_descriptions_to_catalog(filepath):
    """Add descriptions to items in a catalog file."""
    print(f"Processing: {filepath}")
    
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    modified = False
    
    # Handle weapon_types or armor_types structure
    for type_key in ['weapon_types', 'armor_types', 'shield_types']:
        if type_key in data:
            for weapon_type, type_data in data[type_key].items():
                if 'items' in type_data:
                    for item in type_data['items']:
                        slug = item.get('slug', '')
                        if slug and 'description' not in item:
                            # Try to find description
                            desc = (WEAPON_DESCRIPTIONS.get(slug) or 
                                   ARMOR_DESCRIPTIONS.get(slug))
                            if desc:
                                item['description'] = desc
                                modified = True
                                print(f"  Added description for: {item.get('name', slug)}")
    
    # Handle direct items array
    if 'items' in data:
        for item in data['items']:
            slug = item.get('slug', '')
            if slug and 'description' not in item:
                desc = (WEAPON_DESCRIPTIONS.get(slug) or 
                       ARMOR_DESCRIPTIONS.get(slug))
                if desc:
                    item['description'] = desc
                    modified = True
                    print(f"  Added description for: {item.get('name', slug)}")
    
    if modified:
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"  ✓ Updated {filepath}")
    else:
        print(f"  - No changes needed for {filepath}")
    
    return modified

def main():
    # Find all catalog files
    data_dir = Path(__file__).parent.parent / 'RealmEngine.Data' / 'Data' / 'Json' / 'items'
    
    catalog_files = list(data_dir.rglob('catalog.json'))
    
    print(f"Found {len(catalog_files)} catalog files\n")
    
    total_modified = 0
    for catalog_file in catalog_files:
        if add_descriptions_to_catalog(catalog_file):
            total_modified += 1
        print()
    
    print(f"\n✓ Modified {total_modified} files")

if __name__ == '__main__':
    main()

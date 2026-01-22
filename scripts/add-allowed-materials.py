#!/usr/bin/env python3
"""
Add allowedMaterials field to all weapons and armor that don't have it.
"""
import json
import sys
from pathlib import Path

# Material mappings
WEAPON_MATERIALS = {
    # Bladed weapons -> metals
    'heavy-blades': ['@properties/materials/metals:*'],
    'light-blades': ['@properties/materials/metals:*'],
    'axes': ['@properties/materials/metals:*'],
    'polearms': ['@properties/materials/metals:*'],
    
    # Ranged weapons -> woods
    'bows': ['@properties/materials/woods:*'],
    
    # Blunt weapons -> metals (most), woods/stones (clubs)
    'blunt': ['@properties/materials/metals:*'],  # Default, clubs overridden below
    
    # Staves -> woods
    'staves': ['@properties/materials/woods:*'],
}

# Special cases for specific weapon names
WEAPON_NAME_OVERRIDES = {
    'Club': ['@properties/materials/woods:*'],
    'Cudgel': ['@properties/materials/woods:*'],
    'Rod': ['@properties/materials/metals:*', '@properties/materials/gemstones:*'],
    'Scepter': ['@properties/materials/metals:*', '@properties/materials/gemstones:*'],
    'Scepter Staff': ['@properties/materials/metals:*', '@properties/materials/gemstones:*'],
}

ARMOR_MATERIALS = {
    'light-armor': ['@properties/materials/fabrics:*'],
    'medium-armor': ['@properties/materials/leathers:*', '@properties/materials/metals:*'],
    'heavy-armor': ['@properties/materials/metals:*'],
}

# Shields need both woods and metals
ARMOR_NAME_OVERRIDES = {
    'Shield': ['@properties/materials/woods:*', '@properties/materials/metals:*'],
    'Buckler': ['@properties/materials/woods:*', '@properties/materials/metals:*'],
    'Tower Shield': ['@properties/materials/woods:*', '@properties/materials/metals:*'],
    'Kite Shield': ['@properties/materials/woods:*', '@properties/materials/metals:*'],
    'Heater': ['@properties/materials/woods:*', '@properties/materials/metals:*'],
}

def add_allowed_materials(file_path, type_mappings, name_overrides):
    """Add allowedMaterials to items that don't have it."""
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    changes = 0
    item_types = data.get('weapon_types', data.get('armor_types', {}))
    
    for type_key, type_data in item_types.items():
        items = type_data.get('items', [])
        default_materials = type_mappings.get(type_key, [])
        
        for item in items:
            item_name = item.get('name', '')
            
            # Skip if already has allowedMaterials
            if 'allowedMaterials' in item:
                continue
            
            # Check for name-specific override
            materials = name_overrides.get(item_name, default_materials)
            
            if materials:
                # Insert allowedMaterials after armorClass or blockChance if present,
                # otherwise before rarityWeight
                if 'armorClass' in item:
                    # Find position after armorClass
                    keys = list(item.keys())
                    idx = keys.index('armorClass') + 1
                    item_list = list(item.items())
                    item_list.insert(idx, ('allowedMaterials', materials))
                    item.clear()
                    item.update(item_list)
                elif 'blockChance' in item:
                    # Find position after blockChance
                    keys = list(item.keys())
                    idx = keys.index('blockChance') + 1
                    item_list = list(item.items())
                    item_list.insert(idx, ('allowedMaterials', materials))
                    item.clear()
                    item.update(item_list)
                else:
                    # Insert before rarityWeight
                    if 'rarityWeight' in item:
                        keys = list(item.keys())
                        idx = keys.index('rarityWeight')
                        item_list = list(item.items())
                        item_list.insert(idx, ('allowedMaterials', materials))
                        item.clear()
                        item.update(item_list)
                    else:
                        item['allowedMaterials'] = materials
                
                changes += 1
                print(f"  Added allowedMaterials to {type_key}/{item_name}: {materials}")
    
    if changes > 0:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"\n✅ Updated {file_path.name}: {changes} items")
    else:
        print(f"\n✓ No changes needed for {file_path.name}")
    
    return changes

def main():
    repo_root = Path(__file__).parent.parent
    
    weapons_file = repo_root / 'RealmEngine.Data' / 'Data' / 'Json' / 'items' / 'weapons' / 'catalog.json'
    armor_file = repo_root / 'RealmEngine.Data' / 'Data' / 'Json' / 'items' / 'armor' / 'catalog.json'
    
    print("Adding allowedMaterials to weapons...")
    weapon_changes = add_allowed_materials(weapons_file, WEAPON_MATERIALS, WEAPON_NAME_OVERRIDES)
    
    print("\nAdding allowedMaterials to armor...")
    armor_changes = add_allowed_materials(armor_file, ARMOR_MATERIALS, ARMOR_NAME_OVERRIDES)
    
    total = weapon_changes + armor_changes
    print(f"\n{'='*60}")
    print(f"Total items updated: {total}")
    print(f"{'='*60}")

if __name__ == '__main__':
    main()

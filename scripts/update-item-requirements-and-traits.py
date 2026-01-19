import os
import json
import sys

def calculate_requirements(rarity_weight, item_type, skill_reference=None):
    """
    Calculate requirements based on rarityWeight.
    Returns dict with level, attributes, and optional skill.
    """
    requirements = {
        "level": 1,
        "attributes": {}
    }
    
    # Determine rarity tier and base requirements
    if rarity_weight >= 50:  # Common
        requirements["level"] = 1
        base_attr = 8
    elif rarity_weight >= 30:  # Uncommon
        requirements["level"] = 5
        base_attr = 12
    elif rarity_weight >= 15:  # Rare
        requirements["level"] = 10
        base_attr = 16
        if skill_reference:
            requirements["skill"] = {"reference": skill_reference, "rank": 5}
    elif rarity_weight >= 5:  # Epic
        requirements["level"] = 15
        base_attr = 20
        if skill_reference:
            requirements["skill"] = {"reference": skill_reference, "rank": 10}
    else:  # Legendary (1-4)
        requirements["level"] = 20
        base_attr = 24
        if skill_reference:
            requirements["skill"] = {"reference": skill_reference, "rank": 15}
    
    # Determine which attribute is primary based on item type
    if item_type in ["heavy-blades", "axes", "blunt", "polearms", "staves"]:
        requirements["attributes"]["strength"] = base_attr
    elif item_type in ["light-blades", "bows"]:
        requirements["attributes"]["dexterity"] = base_attr
    elif "armor" in item_type or item_type == "shields":
        requirements["attributes"]["constitution"] = base_attr
    
    return requirements

def get_weapon_traits(weapon_type, item_name):
    """
    Get weapon-specific traits based on weapon type.
    """
    traits = {}
    
    # Base traits for weapon types
    if weapon_type == "heavy-blades":
        traits["critChance"] = {"value": 10.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.0, "type": "number"}
        traits["range"] = {"value": 1, "type": "number"}
        traits["cleaveTargets"] = {"value": 0, "type": "number"}
    
    elif weapon_type == "light-blades":
        traits["critChance"] = {"value": 15.0, "type": "number"}
        traits["critMultiplier"] = {"value": 1.8, "type": "number"}
        traits["range"] = {"value": 1, "type": "number"}
        traits["cleaveTargets"] = {"value": 0, "type": "number"}
    
    elif weapon_type == "axes":
        traits["critChance"] = {"value": 8.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.5, "type": "number"}
        traits["range"] = {"value": 1, "type": "number"}
        traits["cleaveTargets"] = {"value": 1, "type": "number"}  # Can hit 2 targets
    
    elif weapon_type == "bows":
        traits["critChance"] = {"value": 12.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.0, "type": "number"}
        # Range varies by bow type
        if "long" in item_name.lower() or "war" in item_name.lower():
            traits["range"] = {"value": 10, "type": "number"}
        elif "short" in item_name.lower() or "hunt" in item_name.lower():
            traits["range"] = {"value": 6, "type": "number"}
        elif "cross" in item_name.lower():
            traits["range"] = {"value": 8, "type": "number"}
        else:
            traits["range"] = {"value": 7, "type": "number"}
        traits["cleaveTargets"] = {"value": 0, "type": "number"}
    
    elif weapon_type == "polearms":
        traits["critChance"] = {"value": 8.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.2, "type": "number"}
        traits["range"] = {"value": 2, "type": "number"}  # Reach weapons
        traits["cleaveTargets"] = {"value": 1, "type": "number"}
    
    elif weapon_type == "blunt":
        traits["critChance"] = {"value": 5.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.8, "type": "number"}
        traits["range"] = {"value": 1, "type": "number"}
        traits["cleaveTargets"] = {"value": 0, "type": "number"}
        # Blunt weapons have chance to stun
        traits["statusEffectChance"] = {"value": 15.0, "type": "number"}
        traits["statusEffectType"] = {"value": "stun", "type": "string"}
    
    elif weapon_type == "staves":
        traits["critChance"] = {"value": 10.0, "type": "number"}
        traits["critMultiplier"] = {"value": 2.0, "type": "number"}
        traits["range"] = {"value": 2, "type": "number"}  # Reach
        traits["cleaveTargets"] = {"value": 0, "type": "number"}
    
    return traits

def get_armor_traits(armor_class, item_name, slot=None):
    """
    Get armor-specific traits based on armor class.
    """
    traits = {}
    
    # Shields get special treatment
    if slot == "offhand" or "shield" in item_name.lower() or "buckler" in item_name.lower():
        traits["blockChance"] = {"value": 20.0, "type": "number"}
        traits["dodgeModifier"] = {"value": -5, "type": "number"}
        traits["damageReduction"] = {"value": 3, "type": "number"}
        return traits
    
    # Regular armor by class
    if armor_class == "light":
        traits["dodgeModifier"] = {"value": 10, "type": "number"}
        traits["damageReduction"] = {"value": 3, "type": "number"}
    
    elif armor_class == "medium":
        traits["dodgeModifier"] = {"value": 5, "type": "number"}
        traits["damageReduction"] = {"value": 7, "type": "number"}
    
    elif armor_class == "heavy":
        traits["dodgeModifier"] = {"value": -5, "type": "number"}
        traits["damageReduction"] = {"value": 12, "type": "number"}
    
    return traits

def update_weapons(base_path):
    """Update weapons catalog with requirements and traits."""
    file_path = os.path.join(base_path, "RealmEngine.Data", "Data", "Json", "items", "weapons", "catalog.json")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    updated_count = 0
    
    for weapon_type, type_data in data.get("weapon_types", {}).items():
        skill_ref = None
        if "properties" in type_data and "skillReference" in type_data["properties"]:
            skill_ref = type_data["properties"]["skillReference"].get("value")
        
        for item in type_data.get("items", []):
            # Remove old attributes field
            if "attributes" in item:
                del item["attributes"]
            
            # Add requirements
            rarity_weight = item.get("rarityWeight", 50)
            item["requirements"] = calculate_requirements(rarity_weight, weapon_type, skill_ref)
            
            # Add weapon-specific traits
            weapon_traits = get_weapon_traits(weapon_type, item.get("name", ""))
            
            # Merge with existing traits (preserve any custom overrides)
            if "traits" not in item or not item["traits"]:
                item["traits"] = {}
            item["traits"].update(weapon_traits)
            
            updated_count += 1
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"✓ Updated weapons/catalog.json ({updated_count} items)")
    return updated_count

def update_armor(base_path):
    """Update armor catalog with requirements and traits."""
    file_path = os.path.join(base_path, "RealmEngine.Data", "Data", "Json", "items", "armor", "catalog.json")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    updated_count = 0
    
    for armor_type, type_data in data.get("armor_types", {}).items():
        skill_ref = None
        if "properties" in type_data and "skillReference" in type_data["properties"]:
            skill_ref = type_data["properties"]["skillReference"].get("value")
        
        for item in type_data.get("items", []):
            # Remove old attributes field
            if "attributes" in item:
                del item["attributes"]
            
            # Add requirements
            rarity_weight = item.get("rarityWeight", 50)
            item["requirements"] = calculate_requirements(rarity_weight, armor_type, skill_ref)
            
            # Add armor-specific traits
            armor_class = item.get("armorClass", "light")
            
            # Check if it's a shield (has blockChance in stats or specific name)
            item_name = item.get("name", "").lower()
            
            # Determine slot from existing data or name
            slot = None
            if "slot" in item.get("traits", {}):
                slot = item["traits"]["slot"].get("value")
            
            armor_traits = get_armor_traits(armor_class, item_name, slot)
            
            # Merge with existing traits
            if "traits" not in item or not item["traits"]:
                item["traits"] = {}
            item["traits"].update(armor_traits)
            
            updated_count += 1
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"✓ Updated armor/catalog.json ({updated_count} items)")
    return updated_count

def update_consumables(base_path):
    """Add stackSize to consumables."""
    file_path = os.path.join(base_path, "RealmEngine.Data", "Data", "Json", "items", "consumables", "catalog.json")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    updated_count = 0
    
    for consumable_type, type_data in data.get("consumable_types", {}).items():
        for item in type_data.get("items", []):
            # Add stackSize if not present
            if "stackSize" not in item:
                item["stackSize"] = 99
                updated_count += 1
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"✓ Updated consumables/catalog.json (added stackSize to {updated_count} items)")
    return updated_count

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.dirname(script_dir)
    
    print("Updating item catalogs with requirements and traits...")
    print(f"Repository root: {repo_root}\n")
    
    try:
        weapons_updated = update_weapons(repo_root)
        armor_updated = update_armor(repo_root)
        consumables_updated = update_consumables(repo_root)
        
        print("\n" + "="*60)
        print("Summary:")
        print(f"  Weapons updated: {weapons_updated}")
        print(f"  Armor updated: {armor_updated}")
        print(f"  Consumables updated: {consumables_updated}")
        print(f"  Total: {weapons_updated + armor_updated + consumables_updated}")
        print("="*60)
        print("\n✓ All item catalogs updated successfully!")
        sys.exit(0)
    except Exception as e:
        print(f"\n✗ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

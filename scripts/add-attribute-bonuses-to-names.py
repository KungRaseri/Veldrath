"""
Add attribute bonuses to thematically appropriate name components in weapons/armor.
Only adds bonuses where the component name suggests a specific attribute.
"""

import json
from pathlib import Path

# Attribute bonus mappings based on thematic keywords
ATTRIBUTE_THEMES = {
    # Strength-themed (power, might, force)
    'strength': ['mighty', 'powerful', 'titanic', 'herculean', 'strong', 'brutal', 'savage', 'iron-willed', 
                 'crushing', 'heavy', 'reinforced', 'hardened', 'forged', 'sturdy'],
    
    # Dexterity-themed (speed, agility, precision)
    'dexterity': ['swift', 'quick', 'agile', 'nimble', 'precise', 'deadly', 'sleek', 'graceful',
                  'shadow', 'silent', 'phantom', 'stealth'],
    
    # Constitution-themed (endurance, fortitude, toughness)
    'constitution': ['fortified', 'resilient', 'enduring', 'tough', 'hardy', 'stalwart', 'vigorous',
                     'guardian', 'defender', 'bulwark', 'iron', 'unyielding'],
    
    # Intelligence-themed (knowledge, arcane, mental)
    'intelligence': ['scholarly', 'arcane', 'tactical', 'calculating', 'brilliant', 'ingenious',
                     'runic', 'mystical', 'eldritch', 'archmage'],
    
    # Wisdom-themed (insight, perception, spiritual)
    'wisdom': ['wise', 'enlightened', 'perceptive', 'sage', 'prophetic', 'insightful',
               'oracle', 'seer', 'druid', 'sacred'],
    
    # Charisma-themed (leadership, charm, presence)
    'charisma': ['noble', 'regal', 'commanding', 'charismatic', 'majestic', 'inspiring', 'glorious',
                 'kingly', 'imperial', 'sovereign', 'radiant']
}

def get_attribute_for_component(component_value: str) -> tuple[str, int] | None:
    """
    Determine if a component should get an attribute bonus based on its name.
    Returns (attribute_name, bonus_amount) or None.
    """
    value_lower = component_value.lower()
    
    for attribute, keywords in ATTRIBUTE_THEMES.items():
        for keyword in keywords:
            if keyword in value_lower:
                # Determine bonus based on rarity (we'll check rarityWeight in calling function)
                return (attribute, 0)  # Bonus amount determined by rarityWeight
    
    return None

def calculate_bonus_amount(rarity_weight: int) -> int:
    """Calculate attribute bonus amount based on rarityWeight."""
    if rarity_weight >= 50:  # Common
        return 2
    elif rarity_weight >= 30:  # Uncommon
        return 3
    elif rarity_weight >= 15:  # Rare
        return 4
    elif rarity_weight >= 5:  # Epic
        return 5
    else:  # Legendary (< 5)
        return 6

def add_attribute_bonuses_to_component(component: dict) -> bool:
    """
    Add attribute bonus trait to a component if thematically appropriate.
    Returns True if bonus was added, False if component already has it or doesn't need it.
    """
    value = component.get('value', '')
    rarity_weight = component.get('rarityWeight', 50)
    traits = component.get('traits', {})
    
    # Check if component should get an attribute bonus
    attribute_info = get_attribute_for_component(value)
    if not attribute_info:
        return False
    
    attribute_name, _ = attribute_info
    bonus_key = f"{attribute_name}Bonus"
    
    # Skip if already has this attribute bonus
    if bonus_key in traits:
        return False
    
    # Calculate bonus amount
    bonus_amount = calculate_bonus_amount(rarity_weight)
    
    # Add the trait
    traits[bonus_key] = {
        "value": bonus_amount,
        "type": "number"
    }
    component['traits'] = traits
    
    print(f"  ✓ Added {bonus_key}: {bonus_amount} to '{value}' (rarityWeight: {rarity_weight})")
    return True

def process_names_file(file_path: Path) -> int:
    """Process a single names.json file and add attribute bonuses where appropriate."""
    print(f"\n=== Processing {file_path.relative_to(file_path.parents[4])} ===")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    modified_count = 0
    
    # Process all component arrays
    components = data.get('components', {})
    for component_type, component_list in components.items():
        if isinstance(component_list, list):
            for component in component_list:
                if isinstance(component, dict) and 'value' in component:
                    if add_attribute_bonuses_to_component(component):
                        modified_count += 1
    
    if modified_count > 0:
        # Write back to file
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"  Modified {modified_count} components")
    else:
        print(f"  No changes needed")
    
    return modified_count

def main():
    """Main entry point."""
    json_root = Path(__file__).parent.parent / 'RealmEngine.Data' / 'Data' / 'Json'
    
    # Target files: weapons and armor names.json
    target_files = [
        json_root / 'items' / 'weapons' / 'names.json',
        json_root / 'items' / 'armor' / 'names.json',
    ]
    
    total_modified = 0
    
    for file_path in target_files:
        if file_path.exists():
            total_modified += process_names_file(file_path)
        else:
            print(f"⚠ File not found: {file_path}")
    
    print(f"\n{'='*60}")
    print(f"Summary: Added attribute bonuses to {total_modified} components")
    print(f"{'='*60}")

if __name__ == '__main__':
    main()

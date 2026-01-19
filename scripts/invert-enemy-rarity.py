import json
import os
from pathlib import Path

def invert_rarity_weights(obj):
    """Recursively invert rarityWeight values in a data structure."""
    if isinstance(obj, dict):
        for key, value in obj.items():
            if key == 'rarityWeight' and isinstance(value, int):
                obj[key] = max(1, 110 - value)
            else:
                invert_rarity_weights(value)
    elif isinstance(obj, list):
        for item in obj:
            invert_rarity_weights(item)

# Process all enemy catalog files
base_path = Path(r'c:\code\console-game\RealmEngine.Data\Data\Json\enemies')
for catalog_file in base_path.rglob('catalog.json'):
    print(f"Processing: {catalog_file.name} in {catalog_file.parent.name}")
    
    with open(catalog_file, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    invert_rarity_weights(data)
    
    with open(catalog_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"  ✓ Updated {catalog_file.name}")

print("\nAll enemy catalogs updated!")

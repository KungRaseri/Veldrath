#!/usr/bin/env python3
"""Remove invalid tokens (descriptive, quality, material) from names.json files."""

import json
import sys
from pathlib import Path

files = [
    'RealmEngine.Data/Data/Json/enemies/beasts/names.json',
    'RealmEngine.Data/Data/Json/enemies/demons/names.json',
    'RealmEngine.Data/Data/Json/enemies/dragons/names.json',
    'RealmEngine.Data/Data/Json/enemies/elementals/names.json',
    'RealmEngine.Data/Data/Json/enemies/goblinoids/names.json',
    'RealmEngine.Data/Data/Json/enemies/humanoids/names.json',
    'RealmEngine.Data/Data/Json/enemies/insects/names.json',
    'RealmEngine.Data/Data/Json/enemies/orcs/names.json',
    'RealmEngine.Data/Data/Json/enemies/plants/names.json',
    'RealmEngine.Data/Data/Json/enemies/reptilians/names.json',
    'RealmEngine.Data/Data/Json/enemies/trolls/names.json',
    'RealmEngine.Data/Data/Json/enemies/undead/names.json',
    'RealmEngine.Data/Data/Json/enemies/vampires/names.json',
    'RealmEngine.Data/Data/Json/items/armor/names.json',
    'RealmEngine.Data/Data/Json/items/consumables/names.json',
    'RealmEngine.Data/Data/Json/items/weapons/names.json',
]

invalid_tokens = {'descriptive', 'quality', 'material'}

for file_path in files:
    path = Path(file_path)
    if not path.exists():
        print(f"⚠️  File not found: {file_path}")
        continue
    
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    # Remove invalid tokens from metadata
    if 'componentKeys' in data.get('metadata', {}):
        original_keys = data['metadata']['componentKeys']
        data['metadata']['componentKeys'] = [k for k in original_keys if k not in invalid_tokens]
        removed_keys = set(original_keys) - set(data['metadata']['componentKeys'])
        if removed_keys:
            print(f"📝 {path.name}: Removed from componentKeys: {removed_keys}")
    
    if 'patternTokens' in data.get('metadata', {}):
        original_tokens = data['metadata']['patternTokens']
        data['metadata']['patternTokens'] = [t for t in original_tokens if t not in invalid_tokens]
        removed_tokens = set(original_tokens) - set(data['metadata']['patternTokens'])
        if removed_tokens:
            print(f"📝 {path.name}: Removed from patternTokens: {removed_tokens}")
    
    # Remove patterns that use invalid tokens
    if 'patterns' in data:
        original_count = len(data['patterns'])
        data['patterns'] = [
            p for p in data['patterns']
            if not any(f'{{{token}}}' in p.get('pattern', '') for token in invalid_tokens)
        ]
        removed_count = original_count - len(data['patterns'])
        if removed_count > 0:
            print(f"🗑️  {path.name}: Removed {removed_count} patterns using invalid tokens")
            data['metadata']['totalPatterns'] = len(data['patterns'])
    
    # Write back with consistent formatting
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"✅ {path.name}: Fixed")

print(f"\n✨ All {len(files)} files processed!")

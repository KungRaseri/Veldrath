#!/usr/bin/env python3
"""
Migrate RealmEngine JSON data from v4.0 to v4.3 unified architecture.

Phase 1: Create new structure (non-breaking)
Phase 2: Update all references (BREAKING)
Phase 3: Migrate names.json files (BREAKING)
Phase 4: Cleanup old structure
"""

import json
import os
import shutil
from pathlib import Path
from typing import Dict, List, Any
from datetime import datetime

# Configuration
DATA_ROOT = Path("RealmEngine.Data/Data/Json")
BACKUP_DIR = Path("backups/v4.0-to-v4.3")

def phase1_create_structure():
    """Phase 1: Create new directories and files (non-breaking)"""
    print("=== Phase 1: Creating New Structure ===")
    
    # Create directories
    (DATA_ROOT / "properties/materials").mkdir(parents=True, exist_ok=True)
    (DATA_ROOT / "properties/qualities").mkdir(parents=True, exist_ok=True)
    (DATA_ROOT / "configuration").mkdir(parents=True, exist_ok=True)
    print("✓ Created directory structure")
    
    # Copy materials/properties/* → properties/materials/*
    src = DATA_ROOT / "materials/properties"
    dst = DATA_ROOT / "properties/materials"
    if src.exists():
        shutil.copytree(src, dst, dirs_exist_ok=True)
        print(f"✓ Copied {src} → {dst}")
    else:
        print(f"⚠ Source directory {src} not found, skipping copy")
    
    # Create properties/qualities/catalog.json
    create_qualities_catalog()
    
    # Create configuration files
    create_material_filters_config()
    create_generation_rules_config()
    create_rarity_config()
    
    # Move/copy general/budget-config.json → configuration/budget.json
    copy_file(
        DATA_ROOT / "general/budget-config.json",
        DATA_ROOT / "configuration/budget.json"
    )
    
    # Move/copy general/socket_config.json → configuration/socket-config.json
    copy_file(
        DATA_ROOT / "general/socket_config.json",
        DATA_ROOT / "configuration/socket-config.json"
    )
    
    # Create .cbconfig.json files
    create_cbconfig(DATA_ROOT / "properties", "Properties", "Package", 10)
    create_cbconfig(DATA_ROOT / "properties/materials", "Materials", "Build", 20)
    create_cbconfig(DATA_ROOT / "properties/qualities", "Qualities", "Star", 30)
    create_cbconfig(DATA_ROOT / "configuration", "Configuration", "Settings", 15)
    
    print("✓ Phase 1 Complete: New structure created\n")

def create_qualities_catalog():
    """Extract quality components from names.json files and create unified catalog"""
    print("Creating properties/qualities/catalog.json...")
    
    # Collect quality components from all names.json files
    qualities = {}
    names_files = [
        DATA_ROOT / "items/weapons/names.json",
        DATA_ROOT / "items/armor/names.json"
    ]
    
    for names_file in names_files:
        if not names_file.exists():
            continue
        
        try:
            with open(names_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
        except Exception as e:
            print(f"⚠ Error reading {names_file}: {e}")
            continue
        
        if 'components' not in data or 'quality' not in data['components']:
            continue
        
        for quality in data['components']['quality']:
            name = quality.get('value') or quality.get('name')
            if name not in qualities:
                qualities[name] = {
                    'name': name,
                    'rarityWeight': quality.get('rarityWeight', 50),
                    'description': quality.get('description', ''),
                    'itemTypeTraits': {}
                }
            
            # Determine item type from file path
            item_type = 'weapon' if 'weapons' in str(names_file) else 'armor'
            
            # Add traits for this item type
            if 'traits' in quality:
                qualities[name]['itemTypeTraits'][item_type] = quality['traits']
    
    # Create catalog
    catalog = {
        'version': '4.3',
        'type': 'quality_catalog',
        'lastUpdated': datetime.now().strftime('%Y-%m-%d'),
        'description': 'Universal quality tiers for all items',
        'qualities': list(qualities.values())
    }
    
    output_path = DATA_ROOT / "properties/qualities/catalog.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(catalog, f, indent=2)
    
    print(f"✓ Created {output_path} with {len(qualities)} qualities")

def create_material_filters_config():
    """Transform material-pools.json → material-filters.json"""
    print("Creating configuration/material-filters.json...")
    
    # Read existing material-pools.json
    pools_path = DATA_ROOT / "general/material-pools.json"
    if not pools_path.exists():
        print(f"⚠ {pools_path} not found, creating default material-filters.json")
        filters = create_default_material_filters()
    else:
        try:
            with open(pools_path, 'r', encoding='utf-8') as f:
                pools_data = json.load(f)
            
            # Transform structure
            filters = transform_material_pools(pools_data)
        except Exception as e:
            print(f"⚠ Error reading material-pools.json: {e}, using default")
            filters = create_default_material_filters()
    
    output_path = DATA_ROOT / "configuration/material-filters.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(filters, f, indent=2)
    
    print(f"✓ Created {output_path}")

def transform_material_pools(pools_data: Dict) -> Dict:
    """Transform old material-pools.json to new material-filters.json format"""
    filters = {
        'version': '4.3',
        'type': 'material_filter_config',
        'lastUpdated': datetime.now().strftime('%Y-%m-%d'),
        'description': 'Item type material compatibility',
        'filters': {}
    }
    
    # Convert pools structure
    if 'pools' in pools_data:
        for pool_name, pool_data in pools_data['pools'].items():
            # Determine item type from pool name
            if 'weapon' in pool_name.lower():
                item_type = 'weapon'
            elif 'armor' in pool_name.lower():
                item_type = 'armor'
            else:
                continue
            
            if item_type not in filters['filters']:
                filters['filters'][item_type] = {
                    'allowedMaterials': [],
                    'pools': {}
                }
            
            # Convert pool data
            tier = 'low_tier' if 'low' in pool_name.lower() else 'high_tier'
            filters['filters'][item_type]['pools'][tier] = {}
            
            for material_type, materials in pool_data.items():
                if not isinstance(materials, list):
                    continue
                    
                converted_materials = []
                for material in materials:
                    if not isinstance(material, dict):
                        continue
                    
                    # Update reference
                    old_ref = material.get('materialRef', '')
                    new_ref = old_ref.replace('@materials/properties/', '@properties/materials/')
                    
                    converted_materials.append({
                        'materialRef': new_ref,
                        'rarityWeight': material.get('rarityWeight') or material.get('selectionWeight', 50)
                    })
                
                if converted_materials:
                    filters['filters'][item_type]['pools'][tier][material_type] = converted_materials
                    
                    # Add to allowedMaterials
                    material_domain = f"@properties/materials/{material_type}"
                    if material_domain not in filters['filters'][item_type]['allowedMaterials']:
                        filters['filters'][item_type]['allowedMaterials'].append(material_domain)
    
    return filters

def create_default_material_filters() -> Dict:
    """Create default material-filters.json if material-pools.json doesn't exist"""
    return {
        'version': '4.3',
        'type': 'material_filter_config',
        'lastUpdated': datetime.now().strftime('%Y-%m-%d'),
        'description': 'Item type material compatibility',
        'filters': {
            'weapon': {
                'allowedMaterials': [
                    '@properties/materials/metals',
                    '@properties/materials/woods'
                ],
                'pools': {}
            },
            'armor': {
                'allowedMaterials': [
                    '@properties/materials/metals',
                    '@properties/materials/leathers'
                ],
                'pools': {}
            }
        }
    }

def create_generation_rules_config():
    """Create configuration/generation-rules.json"""
    print("Creating configuration/generation-rules.json...")
    
    rules = {
        'version': '4.3',
        'type': 'generation_rules_config',
        'lastUpdated': datetime.now().strftime('%Y-%m-%d'),
        'description': 'Item generation rules and limits',
        'componentLimits': {
            'quality': {'min': 0, 'max': 1},
            'material': {'min': 0, 'max': 1},
            'prefixes': {
                'byRarity': {
                    'common': {'min': 0, 'max': 1},
                    'uncommon': {'min': 0, 'max': 1},
                    'rare': {'min': 0, 'max': 2},
                    'epic': {'min': 0, 'max': 2},
                    'legendary': {'min': 0, 'max': 3}
                }
            },
            'suffixes': {
                'byRarity': {
                    'common': {'min': 0, 'max': 0},
                    'uncommon': {'min': 0, 'max': 1},
                    'rare': {'min': 0, 'max': 1},
                    'epic': {'min': 0, 'max': 2},
                    'legendary': {'min': 0, 'max': 3}
                }
            }
        },
        'displayRules': {
            'nameFormat': '[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]',
            'showAllComponents': False,
            'showFirstPrefixOnly': True,
            'showFirstSuffixOnly': True,
            'hideComponentsAboveRarity': None
        },
        'validationRules': {
            'enforceComponentUniqueness': True,
            'allowDuplicatePrefixes': False,
            'allowDuplicateSuffixes': False,
            'allowDuplicateAcrossCategories': True
        }
    }
    
    output_path = DATA_ROOT / "configuration/generation-rules.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(rules, f, indent=2)
    
    print(f"✓ Created {output_path}")

def create_rarity_config():
    """Create configuration/rarity.json"""
    print("Creating configuration/rarity.json...")
    
    rarity = {
        'version': '4.3',
        'type': 'rarity_config',
        'lastUpdated': datetime.now().strftime('%Y-%m-%d'),
        'description': 'Rarity tier definitions and colors',
        'tiers': [
            {
                'name': 'Common',
                'rarityWeightRange': {'min': 50, 'max': 100},
                'color': '#FFFFFF',
                'dropChance': 0.5
            },
            {
                'name': 'Uncommon',
                'rarityWeightRange': {'min': 30, 'max': 49},
                'color': '#1EFF00',
                'dropChance': 0.25
            },
            {
                'name': 'Rare',
                'rarityWeightRange': {'min': 15, 'max': 29},
                'color': '#0070DD',
                'dropChance': 0.15
            },
            {
                'name': 'Epic',
                'rarityWeightRange': {'min': 5, 'max': 14},
                'color': '#A335EE',
                'dropChance': 0.08
            },
            {
                'name': 'Legendary',
                'rarityWeightRange': {'min': 1, 'max': 4},
                'color': '#FF8000',
                'dropChance': 0.02
            }
        ]
    }
    
    output_path = DATA_ROOT / "configuration/rarity.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(rarity, f, indent=2)
    
    print(f"✓ Created {output_path}")

def phase2_update_references():
    """Phase 2: Update all material property references (BREAKING)"""
    print("=== Phase 2: Updating References (BREAKING CHANGE) ===")
    
    reference_map = {
        '@materials/properties/metals': '@properties/materials/metals',
        '@materials/properties/woods': '@properties/materials/woods',
        '@materials/properties/leathers': '@properties/materials/leathers'
    }
    
    # Find all JSON files
    json_files = list(DATA_ROOT.rglob("*.json"))
    updated_count = 0
    
    for json_file in json_files:
        # Skip backup files and new structure
        if 'backup' in str(json_file) or 'properties/materials' in str(json_file):
            continue
        
        try:
            with open(json_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Check if any references need updating
            needs_update = any(old_ref in content for old_ref in reference_map.keys())
            
            if needs_update:
                # Update references
                for old_ref, new_ref in reference_map.items():
                    content = content.replace(old_ref, new_ref)
                
                # Write back
                with open(json_file, 'w', encoding='utf-8') as f:
                    f.write(content)
                
                updated_count += 1
                print(f"✓ Updated {json_file.relative_to(DATA_ROOT)}")
        
        except Exception as e:
            print(f"✗ Error updating {json_file}: {e}")
    
    print(f"✓ Phase 2 Complete: Updated {updated_count} files\n")

def phase3_migrate_names_files():
    """Phase 3: Migrate all names.json files to v4.3 format (BREAKING)"""
    print("=== Phase 3: Migrating names.json Files ===")
    
    names_files = list(DATA_ROOT.rglob("names.json"))
    
    for names_file in names_files:
        if 'backup' in str(names_file):
            continue
        
        migrate_names_file(names_file)
    
    print("✓ Phase 3 Complete: All names.json files migrated\n")

def migrate_names_file(file_path: Path):
    """Migrate a single names.json file to v4.3 format"""
    print(f"Migrating {file_path.relative_to(DATA_ROOT)}...")
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
    except Exception as e:
        print(f"  ✗ Error reading file: {e}")
        return
    
    # Remove quality and material components
    if 'components' in data:
        if 'quality' in data['components']:
            del data['components']['quality']
            print(f"  ✓ Removed quality components")
        
        if 'material' in data['components']:
            del data['components']['material']
            print(f"  ✓ Removed material components")
        
        # Merge descriptive → prefix if exists
        if 'descriptive' in data['components']:
            if 'prefix' not in data['components']:
                data['components']['prefix'] = []
            
            data['components']['prefix'].extend(data['components']['descriptive'])
            del data['components']['descriptive']
            print(f"  ✓ Merged descriptive → prefix")
        
        # Rename value → name in all components
        for component_type in data['components']:
            for component in data['components'][component_type]:
                if 'value' in component:
                    component['name'] = component.pop('value')
    
    # Update metadata
    if 'metadata' not in data:
        data['metadata'] = {}
    
    data['metadata']['version'] = '4.3'
    if 'type' not in data['metadata']:
        data['metadata']['type'] = 'modifier_catalog'
    data['metadata']['lastUpdated'] = datetime.now().strftime('%Y-%m-%d')
    
    # Remove old root-level fields if they exist
    if 'version' in data:
        del data['version']
    if 'type' in data:
        del data['type']
    if 'lastUpdated' in data:
        del data['lastUpdated']
    if 'description' in data:
        del data['description']
    
    # Write back
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
        print(f"  ✓ Migrated to v4.3")
    except Exception as e:
        print(f"  ✗ Error writing file: {e}")

def phase4_cleanup():
    """Phase 4: Remove old structure"""
    print("=== Phase 4: Cleanup Old Structure ===")
    
    # Delete materials/properties/ (moved to properties/materials/)
    old_materials = DATA_ROOT / "materials/properties"
    if old_materials.exists():
        shutil.rmtree(old_materials)
        print(f"✓ Deleted {old_materials}")
    
    # Delete general/budget-config.json (moved to configuration/)
    old_budget = DATA_ROOT / "general/budget-config.json"
    if old_budget.exists():
        old_budget.unlink()
        print(f"✓ Deleted {old_budget}")
    
    # Delete general/socket_config.json (moved to configuration/)
    old_socket = DATA_ROOT / "general/socket_config.json"
    if old_socket.exists():
        old_socket.unlink()
        print(f"✓ Deleted {old_socket}")
    
    # Delete general/material-pools.json (replaced by material-filters.json)
    old_pools = DATA_ROOT / "general/material-pools.json"
    if old_pools.exists():
        old_pools.unlink()
        print(f"✓ Deleted {old_pools}")
    
    print("✓ Phase 4 Complete: Cleanup finished\n")

def create_cbconfig(dir_path: Path, display_name: str, icon: str, sort_order: int):
    """Create .cbconfig.json file for ContentBuilder UI"""
    config = {
        'displayName': display_name,
        'icon': icon,
        'sortOrder': sort_order,
        'description': f'{display_name} directory'
    }
    
    config_path = dir_path / ".cbconfig.json"
    config_path.parent.mkdir(parents=True, exist_ok=True)
    with open(config_path, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2)
    print(f"✓ Created {config_path.relative_to(DATA_ROOT)}")

def copy_file(src: Path, dst: Path):
    """Copy a file and create parent directories if needed"""
    if not src.exists():
        print(f"⚠ {src} not found, skipping")
        return
    
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(str(src), str(dst))
    print(f"✓ Copied {src.name} → {dst}")

def create_backup():
    """Create backup of current JSON data"""
    print("Creating backup...")
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)
    
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    backup_path = BACKUP_DIR / f"Json_{timestamp}"
    
    shutil.copytree(DATA_ROOT, backup_path, dirs_exist_ok=True)
    print(f"✓ Backup created at {backup_path}\n")
    return backup_path

def main():
    """Run migration script"""
    import argparse
    
    parser = argparse.ArgumentParser(description='Migrate to JSON v4.3 unified architecture')
    parser.add_argument('--phase', type=int, choices=[1, 2, 3, 4], help='Run specific phase only')
    parser.add_argument('--all', action='store_true', help='Run all phases')
    parser.add_argument('--backup', action='store_true', help='Create backup before running')
    
    args = parser.parse_args()
    
    if args.backup:
        backup_path = create_backup()
        print(f"Backup location: {backup_path}")
        print("You can restore from backup if needed:\n")
        print(f"  Remove-Item -Recurse -Force {DATA_ROOT}")
        print(f"  Copy-Item -Recurse {backup_path} {DATA_ROOT}")
        print()
    
    if args.phase == 1 or args.all:
        phase1_create_structure()
    
    if args.phase == 2 or args.all:
        phase2_update_references()
    
    if args.phase == 3 or args.all:
        phase3_migrate_names_files()
    
    if args.phase == 4 or args.all:
        phase4_cleanup()
    
    if not args.phase and not args.all and not args.backup:
        print("Usage: python migrate-to-v4.3-unified-architecture.py [--phase 1-4] [--all] [--backup]")
        print("\nPhases:")
        print("  1: Create new structure (non-breaking)")
        print("  2: Update references (BREAKING)")
        print("  3: Migrate names.json files (BREAKING)")
        print("  4: Cleanup old structure")
        print("\nOptions:")
        print("  --backup    Create timestamped backup before migration")
        print("  --phase N   Run specific phase (1-4)")
        print("  --all       Run all phases sequentially")
        print("\nExample:")
        print("  python migrate-to-v4.3-unified-architecture.py --backup --phase 1")

if __name__ == '__main__':
    main()

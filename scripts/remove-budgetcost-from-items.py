import os
import json
import sys

def remove_budgetcost_from_items(base_path):
    """
    Remove budgetCost field from all item catalog.json files.
    Preserves rarityWeight and all other fields.
    """
    items_dir = os.path.join(base_path, "RealmEngine.Data", "Data", "Json", "items")
    
    if not os.path.exists(items_dir):
        print(f"Error: Items directory not found at {items_dir}")
        return False
    
    updated_files = []
    total_removed = 0
    
    # Walk through all subdirectories in items/
    for root, dirs, files in os.walk(items_dir):
        for file in files:
            if file == "catalog.json":
                file_path = os.path.join(root, file)
                relative_path = os.path.relpath(file_path, items_dir)
                
                try:
                    # Read the file
                    with open(file_path, 'r', encoding='utf-8') as f:
                        data = json.load(f)
                    
                    # Track if we made changes
                    changed = False
                    removed_count = 0
                    
                    # Process the catalog structure
                    # Handle different catalog structures (flat items[] or grouped categories)
                    if 'items' in data:
                        # Flat structure: { "items": [...] }
                        for item in data['items']:
                            if 'budgetCost' in item:
                                del item['budgetCost']
                                removed_count += 1
                                changed = True
                    
                    # Check for grouped structure (like weapons: weapon_types -> items)
                    for key, value in data.items():
                        if isinstance(value, dict):
                            # Check if this is a category with items
                            if 'items' in value:
                                for item in value['items']:
                                    if 'budgetCost' in item:
                                        del item['budgetCost']
                                        removed_count += 1
                                        changed = True
                            # Check for nested structures (weapon_types, armor_types, etc.)
                            else:
                                for subkey, subvalue in value.items():
                                    if isinstance(subvalue, dict) and 'items' in subvalue:
                                        for item in subvalue['items']:
                                            if 'budgetCost' in item:
                                                del item['budgetCost']
                                                removed_count += 1
                                                changed = True
                    
                    # Write back if changed
                    if changed:
                        with open(file_path, 'w', encoding='utf-8') as f:
                            json.dump(data, f, indent=2, ensure_ascii=False)
                        
                        updated_files.append(relative_path)
                        total_removed += removed_count
                        print(f"✓ Updated {relative_path} (removed {removed_count} budgetCost fields)")
                    else:
                        print(f"  Skipped {relative_path} (no budgetCost fields found)")
                
                except Exception as e:
                    print(f"✗ Error processing {relative_path}: {e}")
                    return False
    
    print("\n" + "="*60)
    print(f"Summary:")
    print(f"  Files updated: {len(updated_files)}")
    print(f"  Total budgetCost fields removed: {total_removed}")
    print("="*60)
    
    if updated_files:
        print("\nUpdated files:")
        for file in updated_files:
            print(f"  - {file}")
    
    return True

if __name__ == "__main__":
    # Get the repository root (script is in scripts/)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.dirname(script_dir)
    
    print("Removing budgetCost from item catalogs...")
    print(f"Repository root: {repo_root}\n")
    
    success = remove_budgetcost_from_items(repo_root)
    
    if success:
        print("\n✓ All item catalogs updated successfully!")
        sys.exit(0)
    else:
        print("\n✗ Failed to update item catalogs")
        sys.exit(1)

import json
import os
import sys
from pathlib import Path

def format_json_file(filepath):
    """Format a JSON file with 2-space indentation"""
    try:
        # Read with UTF-8 encoding
        with open(filepath, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)
        
        # Write back with 2-space indentation, UTF-8 no BOM
        with open(filepath, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        
        return True
    except Exception as e:
        print(f"  ERROR: {filepath.name} - {e}")
        return False

def main():
    json_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("Data")
    
    if not json_dir.exists():
        print(f"ERROR: Directory not found: {json_dir}")
        return
    
    print(f"Formatting JSON files in: {json_dir}\n")
    
    files_processed = 0
    files_formatted = 0
    
    for json_file in json_dir.rglob("*.json"):
        files_processed += 1
        if format_json_file(json_file):
            files_formatted += 1
            print(f"  ✓ {json_file.name}")
    
    print(f"\nFiles processed: {files_processed}")
    print(f"Files formatted: {files_formatted}")

if __name__ == "__main__":
    main()

import json
import sys

# Read the snapshot file
data = json.load(open(r'E:\Project\Demo\monsterhunter\Library\PSDLayoutTool2\HierarchySnapshots\c6d75b7826ac4688bd4646847f5ee6b032146f3fe25511e8bdb5a06408c756b4.json', 'r', encoding='utf-8'))

# Create a dictionary of nodes by ID
nodes = {n['id']: n for n in data.get('nodes', [])}

# Print comprehensive review
print("=== PREFAB HIERARCHY CLEANUP REVIEW ===")
print(f"\nPrefab: {data.get('prefabAssetPath')}")
print(f"Fingerprint: {data.get('fingerprint')}")
print(f"Total Nodes: {len(data.get('nodes', []))}")
print(f"Component Family Candidates: {len(data.get('componentFamilyCandidates', []))}")
print(f"Containment Findings: {len(data.get('containmentFindings', []))}")
print(f"Flat Sibling Findings: {len(data.get('flatSiblingFindings', []))}")

print("\n=== CURRENT HIERARCHY ISSUES ===")
print("1. Flat Structure: Main container (n000001) has 39 direct children")
print("2. No semantic grouping for related UI elements")
print("3. PSD export names throughout (ui_daily_*, daily_*, Common_Texture_*)")
print("4. Mixed content types in same level (backgrounds, buttons, text, icons)")

print("\n=== PROPOSED SEMANTIC GROUPING ===")
proposed_groups = {
    "Screen Root (n000000)": "Root container, keep as-is",
    "Main Container (n000001)": "Rename to [Screen], add semantic children",
    "Background (n000031-n000034)": "4 background images, group together",
    "Day Navigation Bar (n000003-n000030)": "28 nodes for day selection UI",
    "Progress Section (n000035)": "23 nodes for progress tracking",
    "Task List (n000059)": "5 day containers with task items",
    "Bottom Bar (n000101-n000104)": "Timer and button elements"
}

for group, desc in proposed_groups.items():
    print(f"  {group}: {desc}")

print("\n=== FLAT SIBLING FINDINGS RESOLUTION ===")
for finding in data.get('flatSiblingFindings', []):
    print(f"\n{finding['id']}:")
    print(f"  Background: {finding['background']}")
    print(f"  Members: {finding['members']}")
    print(f"  Resolution: Create wrapper group")

print("\n=== COMPONENT EXTRACTION CANDIDATES ===")
print("No component family candidates identified in snapshot")

print("\n=== VERIFICATION REQUIREMENTS ===")
print("1. Preserve all RectTransform world corners within 0.01")
print("2. Maintain component counts: 105 RectTransform, 96 CanvasRenderer, 59 Image, 37 TextMeshProUGUI")
print("3. Preserve all nested Prefab boundaries")
print("4. Keep all serialized references intact")
print("5. Rename all PSD/export names to semantic English names")

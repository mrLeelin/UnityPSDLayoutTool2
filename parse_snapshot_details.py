import json
import sys

# Read the snapshot file
data = json.load(open(r'E:\Project\Demo\monsterhunter\Library\PSDLayoutTool2\HierarchySnapshots\c6d75b7826ac4688bd4646847f5ee6b032146f3fe25511e8bdb5a06408c756b4.json', 'r', encoding='utf-8'))

# Create a dictionary of nodes by ID
nodes = {n['id']: n for n in data.get('nodes', [])}

# Print detailed information for key nodes
print('=== Key Node Details ===')
key_nodes = ['n000000', 'n000001', 'n000035', 'n000059', 'n000060', 'n000064', 'n000067', 'n000075', 'n000087']
for node_id in key_nodes:
    node = nodes.get(node_id)
    if node:
        print(f"\n{node_id}: {node['name']}")
        print(f"  Components: {node['components']}")
        print(f"  Children: {node['childCount']}")
        print(f"  Size: {node['rect']['sizeDelta']}")
        print(f"  Position: {node['rect']['anchoredPosition']}")
        print(f"  World Rect: {node['worldRect']}")

# Print all leaf nodes with their parent
print('\n=== All Leaf Nodes ===')
for node in data.get('nodes', []):
    if node['childCount'] == 0:
        parent = nodes.get(node['parentId'])
        parent_name = parent['name'] if parent else 'None'
        print(f"{node['id']}: {node['name']} (parent: {parent_name}, pos: {node['rect']['anchoredPosition']}, size: {node['rect']['sizeDelta']})")

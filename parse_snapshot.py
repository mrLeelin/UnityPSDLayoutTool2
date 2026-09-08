import json
import sys

# Read the snapshot file
data = json.load(open(r'E:\Project\Demo\monsterhunter\Library\PSDLayoutTool2\HierarchySnapshots\c6d75b7826ac4688bd4646847f5ee6b032146f3fe25511e8bdb5a06408c756b4.json', 'r', encoding='utf-8'))

# Create a dictionary of nodes by ID
nodes = {n['id']: n for n in data.get('nodes', [])}

# Print hierarchy
print('Hierarchy:')
def print_tree(node_id, indent=0):
    node = nodes[node_id]
    print(' ' * indent + f"{node['id']}: {node['name']} (children: {node['childCount']})")
    children = [n for n in nodes.values() if n['parentId'] == node_id]
    children.sort(key=lambda x: x['siblingIndex'])
    for child in children:
        print_tree(child['id'], indent + 2)

print_tree('n000000')

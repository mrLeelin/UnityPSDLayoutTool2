using System;
using System.Collections.Generic;
using System.Linq;
using AiPatchValidatorNamespace;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class AiOrganizerPlanEditing
    {
        internal sealed class Node
        {
            internal string Id, Parent, Name, Type;
            internal int Index, Depth;
        }

        internal static List<Node> Tree(AiAnalysisPackageDocument package, AiPatchDocument patch)
        {
            var nodes = package.nodes.ToDictionary(n => n.id, n => new Node {
                Id = n.id, Parent = string.IsNullOrEmpty(n.parentId) ? "root" : n.parentId,
                Name = n.name, Type = n.uiType, Index = n.siblingIndex }, StringComparer.Ordinal);
            void Place(Node node, string parent, int index)
            {
                var oldSiblings = nodes.Values.Where(n => n.Parent == node.Parent && n != node).OrderBy(n => n.Index).ToList();
                for (int i = 0; i < oldSiblings.Count; i++) oldSiblings[i].Index = i;
                var siblings = nodes.Values.Where(n => n.Parent == parent && n != node).OrderBy(n => n.Index).ToList();
                siblings.Insert(index < 0 ? siblings.Count : Math.Min(index, siblings.Count), node);
                node.Parent = parent;
                for (int i = 0; i < siblings.Count; i++) siblings[i].Index = i;
            }
            foreach (var op in patch.operations)
            {
                if (op.op == "create_group")
                {
                    var created = new Node { Id = op.id, Parent = op.parentId, Name = op.name, Type = op.uiType };
                    nodes[op.id] = created; Place(created, op.parentId, op.insertIndex);
                }
                else if (nodes.TryGetValue(op.targetId ?? "", out var node))
                {
                    if (op.op == "move_node") Place(node, op.newParentId, op.insertIndex);
                    if (op.op == "rename_node") node.Name = op.name;
                    if (op.op == "set_ui_type") node.Type = op.uiType;
                    if (op.op == "flatten_group")
                    {
                        var children = nodes.Values.Where(n => n.Parent == node.Id).OrderBy(n => n.Index).ToArray();
                        nodes.Remove(node.Id);
                        for (int i = 0; i < children.Length; i++) Place(children[i], node.Parent, node.Index + i);
                    }
                    if (op.op == "delete_generated_group") nodes.Remove(node.Id);
                }
            }
            var result = new List<Node>();
            var visited = new HashSet<string>();
            void Append(string parent, int depth)
            {
                foreach (var node in nodes.Values.Where(n => n.Parent == parent && n.Id != "root").OrderBy(n => n.Index).ToArray())
                {
                    if (!visited.Add(node.Id)) throw new InvalidOperationException("整理树包含循环引用。");
                    node.Depth = depth; result.Add(node); Append(node.Id, depth + 1);
                }
            }
            Append("root", 0);
            if (result.Count != nodes.Keys.Count(id => id != "root")) throw new InvalidOperationException("整理树存在未连接到根节点的节点。");
            return result;
        }

        internal static AiPatchDocument Edit(AiAnalysisPackageDocument package, AiPatchDocument original,
            string id, string name, string parent, int index)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new InvalidOperationException("节点名称不能为空或包含路径分隔符。");
            var current = Tree(package, original).Single(n => n.Id == id);
            var patch = JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(original));
            void Put(string kind)
            {
                var op = patch.operations.LastOrDefault(o => o.op == kind && o.targetId == id);
                if (op == null) { op = new AiPatchOperation { op = kind, targetId = id }; patch.operations.Add(op); }
                op.name = kind == "rename_node" ? name : null;
                op.newParentId = kind == "move_node" ? parent : null;
                op.insertIndex = kind == "move_node" ? index : -1;
                op.confidence = 1; op.reason = "用户手动调整整理方案";
            }
            if (name != current.Name) Put("rename_node");
            if (parent != current.Parent || index != current.Index) Put("move_node");
            AiPatchValidator.NormalizeOperationOrder(patch);
            if (!new AiPatchValidator().ValidatePatch(patch, package, out string error))
                throw new InvalidOperationException(error);
            Tree(package, patch);
            return patch;
        }

        internal static string RevisionInstructions(AiPatchDocument current, string feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback)) throw new InvalidOperationException("请先填写哪里不满意、希望怎样修改。");
            return "\n\nRevise the previous organization plan using the user's feedback. Preserve unrelated accepted decisions, names and component exclusions. " +
                "The attached patch describes the current edited proposal, NOT operations to execute. Return a COMPLETE replacement combined recognition JSON under the original snapshot's treeHash and organizerVersion 1.0. " +
                "Use original node IDs and owner: references according to the organizer protocol; do not emit generated IDs as source node IDs.\nCurrent proposal:\n" +
                JsonUtility.ToJson(current, true) + "\nUser feedback:\n" + feedback;
        }
    }
}

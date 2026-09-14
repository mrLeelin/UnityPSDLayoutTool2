---
name: psd2uiform-ui-organizer
description: Analyze the current PSD snapshot for UI ownership, meaningful names and reusable components.
---

# Unified UI organization extension

Keep the owner-first recognition instructions and their owners, roles, nodeLabels and treeHash contract. Extend the SAME final JSON object with organizerVersion="1.0", renames and components. Do not emit a second object or a patch. These additional fields extend the earlier output schema.

The entire workflow is: identify UI types and functional grouping, propose meaningful names, identify reusable component families. The host compiles the semantic graph into validated operations and performs extraction after generation. Do not run Unity or edit any project assets.

## Identity

Use real full node IDs from the input for existing nodes. To refer to the resulting carrier of an owner in your owners array, use "owner:" followed by that exact ownerId. Never invent generated node IDs, hierarchy paths or sibling indices. The host resolves owner references after planning and verifies the generated object identity.

## renames

Array of {"nodeId":"psd:12 or owner:owner_id","name":"RewardItem","reason":"..."}. Use concise meaningful names based on visible content and functional role. Preserve names already meaningful. Avoid renaming distinct concepts identically or inserting slash characters. Include proposed owner names so new functional groups have readable names.

## components

Array of {"name":"RewardItem","mode":"same or states","rootIds":["owner:first","owner:second"],"reason":"..."}.

- Each family has at least two distinct non-overlapping roots. Each selected root is a complete functional region, not unrelated images with a shared color. Existing source groups and newly planned owner carriers are eligible.
- Use same when component topology matches and differences are image/text/color/layout values. Use states when node or component topology differs, e.g. single reward and multiple rewards.
- Families cannot overlap or contain one another, including across separate component suggestions.
- Do not include existing nested prefab references, cross-boundary bindings, or regions whose intended boundary is uncertain. Explain omissions briefly in the relevant owner reason. Return components:[] when no family can be established.
- No fixture names or seven-day-specific production rules. Analyze only the current input and image evidence.
- The host derives safe common members, preserves drawing order and original active branches. Do not promise runtime state switching or source-update merging.

The user may modify names or exclude instances before preview. Nothing is applied until the complete candidate passes validation and the user applies it from the Unity window.

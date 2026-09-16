# Prefab visual and texture ownership audit

`scripts/prefab_visual_audit.py` is a read-only helper. It does not launch Unity
and does not save a Prefab. It generates C# payloads for the already selected
NativeUnity `eval_file` backend, and it compares captured images locally.

All paths, render settings, ownership search roots, and reviewed state overrides
are caller inputs. Do not put fixture-specific paths, object names, dimensions,
or expected counts into this script.

## Capture payload

Choose camera settings from the reviewed target and reuse exactly the same
arguments for before/after captures:

```powershell
python scripts/prefab_visual_audit.py capture-payload `
  --prefab-asset-path "Assets/UI/RewardPanel.prefab" `
  --image-output-path "Library/PrefabAudit/reward-before.png" `
  --payload-output "Library/PrefabAudit/reward-before.cs" `
  --width 1080 --height 1920 `
  --camera-position 0 0 -2000 `
  --orthographic-size 960 `
  --near-clip 0.1 --far-clip 4000 `
  --background-rgba 0.12 0.12 0.12 1
```

The payload loads transient Prefab contents, renders them, restores any reviewed
active-state overrides, and unloads the contents in `finally`. It contains no
save call.

State coverage is explicit. Add `--state-override
"[States]/Claimable=true"` only when that exact override was reviewed. Capture
each reviewed state combination separately and record its arguments. The helper
does not invent branches, infer default states, or toggle unreviewed objects.

## Strict RGBA comparison

```powershell
python scripts/prefab_visual_audit.py compare `
  --before "Library/PrefabAudit/reward-before.png" `
  --after "Library/PrefabAudit/reward-after.png" `
  --diff-output "Library/PrefabAudit/reward-diff.png" `
  --report-output "Library/PrefabAudit/reward-compare.json"
```

Exit code `0` means identical dimensions and identical red, green, blue, and
alpha channels. Exit code `1` means a dimension or pixel mismatch. The JSON
report includes the reason, changed-pixel count, changed-channel count, maximum
channel delta, and difference bounding box. Exit code `2` means invalid input
or an I/O error. A mismatch blocks completion; do not weaken this comparison to
make a cleanup pass.

## Texture ownership payload

```powershell
python scripts/prefab_visual_audit.py ownership-payload `
  --texture-directory "Assets/UI/RewardPanel/Textures" `
  --owner-root "Assets" `
  --payload-output "Library/PrefabAudit/reward-owners.cs"
```

The generated payload searches current `AssetDatabase` dependencies recursively
under every supplied owner root. Its stable tab-separated report lists each
Texture GUID, asset path, owner count, and sorted owner paths. Recursive owners
are intentionally conservative: an indirect reference still prevents an asset
from being treated as proven-private without review.

Generate the report before a rename and record shared or externally owned
exceptions. The payload never calls `RenameAsset`; the approved cleanup runner
remains responsible for GUID-preserving asset renames and verification.

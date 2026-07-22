# PSD Text Effect Conversion Design

## Goal

Move PSD-to-TMP outline conversion out of the material factory so artists and programmers can adjust the conversion constants in one obvious location without touching asset creation or reuse logic.

## Design

Add `PsdTextEffectConversion`, an editor-only static class beside the PSD Prefab material code.

The top of the class exposes three documented constants as the manual adjustment surface:

- `OutlineScale = 7f / 3f`
- `OutlineDecimalPlaces = 2`
- `FaceDilateRatio = 0.5f`

The class owns two pure conversions:

- PSD stroke pixels plus PSD font size to normalized TMP outline width.
- TMP outline width to TMP face dilate.

`PsdPrefabTextMaterialFactory` continues to own material comparison, reuse, and creation. It must not modify or globally save existing material assets.

## Verification

- Existing Figma-aligned examples remain unchanged: `36/3 -> 0.19`, `48/3 -> 0.15`, `30/3 -> 0.23`, `28/3 -> 0.25`, `28/2 -> 0.17`.
- Face dilate remains half the normalized outline.
- Creating a new variant does not save an existing dirty material.
- Unity compilation completes without errors.

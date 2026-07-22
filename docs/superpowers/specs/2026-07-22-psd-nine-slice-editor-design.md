# PSD Nine-Slice Editor Design

## Goal

Let an artist open one PSD from the PSD Layout Tool inspector, review every visible raster layer, drag or type the four nine-slice borders, and have the selected values survive future incremental PSD-to-Prefab imports.

## Boundaries

- The editor works from the original PSD and does not require a sidecar JSON file.
- Overrides are stored in the selected PSD asset's Unity `.meta` importer `userData`, keyed by Photoshop `layer.Id`.
- The tool does not edit PSD pixels or the embedded Photoshop XMP metadata.
- Existing generated-PNG analysis remains available when a PNG is selected.

## Modules

1. `PsdNineSliceOverrideStore` owns a versioned, line-based `userData` record. It preserves other tools' lines, differentiates no override from an explicit disabled override, and reads/writes borders in author order: left, top, right, bottom.
2. `PsdNineSlicePsdLayerSession` opens a PSD, filters visible non-text leaf layers with pixels, and lazily decodes preview textures. It owns the preview texture lifetime.
3. `PsdNineSliceWindow` renders the PSD layer list and selected-layer canvas. Its preview has four draggable guides, numeric border fields, automatic candidate, save, and clear actions.
4. `PsdImporter` reads overrides once per import. Manual override has priority over layer-name tags and embedded XMP. An explicit disabled override suppresses any automatic/name/XMP nine-slice handling for that layer.

## Interaction

1. In the PSD inspector, click **打开九宫图工具**.
2. Select an image layer in the left list.
3. Enable nine-slice, click **使用自动推断** as a starting point if desired, then drag the four preview guides or type exact pixels.
4. Click **保存当前图层**. The state is written to the PSD `.meta` only.
5. Click **生成预制体** or run incremental import. The importer crops the generated PNG and applies the saved Sprite border using the manual rule.
6. **清除手动覆盖** returns the layer to name-tag/XMP/automatic behavior.

## Priority

`manual enabled/disabled override` > `PSD layer-name rule` > `embedded XMP border` > `no nine-slice`.

Layer IDs are retained by Photoshop during ordinary position, size, pixel, and name edits, so stored overrides participate in incremental update. When a layer is deleted and recreated Photoshop assigns a new ID; the editor intentionally treats that as a new layer rather than applying a possibly wrong old border.

## Verification

- Unit tests prove storage round-trip, external `userData` preservation, manual-disabled behavior, and manual-priority resolution.
- Unity compilation has zero errors.
- A Unity dynamic smoke script opens the PSD editor for `7日任务拆分.psd`, verifies visible raster layers load, and verifies an override written to the PSD importer can be read back.

const { hashString, numberOr, stableKeys } = require("../shared/values");
const { readLegacyNineSlice } = require("../nine-slice/nineSliceValue");

function buildManifest(document, nineSliceByLayerId, documentGateway) {
  const layers = [];
  const walk = (sourceLayers, parentId, parentPath) => {
    sourceLayers.forEach((layer, siblingIndex) => {
      const layerId = String(layer.id);
      const path = parentPath.concat([siblingIndex]);
      const record = {
        layerId,
        parentId: parentId || null,
        siblingIndex,
        path,
        name: layer.name || "",
        kind: String(layer.kind || "unknown"),
        visible: Boolean(layer.visible),
        opacity: numberOr(layer.opacity, 100),
        bounds: documentGateway.readBounds(layer.bounds),
        text: documentGateway.readText(layer),
        nineSlice: nineSliceByLayerId && nineSliceByLayerId.has(layerId)
          ? nineSliceByLayerId.get(layerId)
          : readLegacyNineSlice(layer),
        fingerprint: ""
      };
      record.fingerprint = hashString(JSON.stringify(record, stableKeys(record)));
      layers.push(record);

      if (layer.layers && layer.layers.length) {
        walk(layer.layers, layerId, path);
      }
    });
  };

  walk(document.layers, null, []);
  const documentCore = {
    width: numberOr(document.width, 0),
    height: numberOr(document.height, 0),
    resolution: numberOr(document.resolution, 72),
    layers: layers.map(layer => ({ layerId: layer.layerId, fingerprint: layer.fingerprint }))
  };

  return {
    schema: "psd-unity-layout",
    schemaVersion: 1,
    nineSliceSchemaVersion: 1,
    source: "photoshop-uxp",
    document: {
      name: document.name || "",
      width: documentCore.width,
      height: documentCore.height,
      resolution: documentCore.resolution
    },
    documentFingerprint: hashString(JSON.stringify(documentCore, stableKeys(documentCore))),
    layers
  };
}

module.exports = { buildManifest };

const uxp = require("uxp");
const { fromBase64Utf8, toBase64Utf8 } = require("../shared/values");
const { normalizeNineSlice } = require("../nine-slice/nineSliceValue");

const xmp = uxp.xmp;
const namespace = "https://codex.openai.com/psd-layout/1.0/";
const prefix = "psdUnity";

function collectStoredNineSlices(manifest) {
  const result = new Map();
  if (!manifest || !Array.isArray(manifest.layers)) {
    return result;
  }

  manifest.layers.forEach(layer => {
    if (layer && layer.layerId && layer.nineSlice) {
      result.set(String(layer.layerId), normalizeNineSlice(layer.nineSlice));
    }
  });
  return result;
}

function readManifestForDocument(document) {
  return withXmpFile(document, metadata => readEmbeddedManifest(metadata));
}

function writeManifestForDocument(document, manifest) {
  return withXmpFile(document, metadata => {
    xmp.XMPMeta.registerNamespace(namespace, prefix);
    metadata.setProperty(namespace, "schemaVersion", "1");
    metadata.setProperty(namespace, "encoding", "base64+utf8");
    metadata.setProperty(namespace, "manifest", toBase64Utf8(JSON.stringify(manifest)));
    metadata.setProperty(namespace, "manifestFingerprint", manifest.documentFingerprint);
    return manifest;
  }, true);
}

function withXmpFile(document, action, writeBack) {
  let xmpFile;
  try {
    xmpFile = new xmp.XMPFile(
      document.path,
      xmp.XMPConst.FILE_PHOTOSHOP,
      xmp.XMPConst.OPEN_FOR_UPDATE
    );
    if (!xmpFile) {
      throw new Error("Unable to open PSD XMP metadata.");
    }

    const metadata = xmpFile.getXMP() || new xmp.XMPMeta();
    const result = action(metadata);
    if (writeBack) {
      xmpFile.putXMP(metadata);
    }
    return result;
  } finally {
    if (xmpFile) {
      xmpFile.closeFile(xmp.XMPConst.CLOSE_UPDATE_SAFELY);
    }
  }
}

function readEmbeddedManifest(metadata) {
  try {
    const property = metadata.getProperty(namespace, "manifest");
    const encoded = property && (property.value || property);
    return typeof encoded === "string" && encoded.length > 0
      ? JSON.parse(fromBase64Utf8(encoded))
      : null;
  } catch (_) {
    return null;
  }
}

module.exports = {
  collectStoredNineSlices,
  readManifestForDocument,
  writeManifestForDocument
};

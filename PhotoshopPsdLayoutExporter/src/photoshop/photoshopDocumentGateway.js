const photoshop = require("photoshop");
const { numberOr, stringOr } = require("../shared/values");

const app = photoshop.app;
const core = photoshop.core;

function getActiveDocument() {
  const document = app.activeDocument;
  if (!document) {
    throw new Error("Open a Photoshop document first.");
  }
  if (!document.path || document.path.startsWith("cloud:")) {
    throw new Error("Save the document as a local PSD or PSB file first.");
  }
  return document;
}

function getSingleSelectedLayer(document) {
  const layers = document.activeLayers ? Array.from(document.activeLayers) : [];
  if (layers.length !== 1) {
    throw new Error("Select exactly one Photoshop layer before applying 9-slice.");
  }
  return layers[0];
}

function readBounds(bounds) {
  if (!bounds) {
    return { x: 0, y: 0, width: 0, height: 0 };
  }
  return {
    x: numberOr(bounds.left, 0),
    y: numberOr(bounds.top, 0),
    width: numberOr(bounds.width, 0),
    height: numberOr(bounds.height, 0)
  };
}

function readText(layer) {
  try {
    if (!layer.textItem) {
      return null;
    }
    const textItem = layer.textItem;
    const characterStyle = textItem.characterStyle || {};
    const paragraphStyle = textItem.paragraphStyle || {};
    return {
      contents: textItem.contents || "",
      isPointText: Boolean(textItem.isPointText),
      isParagraphText: Boolean(textItem.isParagraphText),
      fontSize: numberOr(characterStyle.size, 0),
      fontName: stringOr(characterStyle.font, ""),
      justification: stringOr(paragraphStyle.justification, "")
    };
  } catch (_) {
    return null;
  }
}

async function saveIfDirty(document) {
  if (document.saved) {
    return;
  }
  await core.executeAsModal(
    async () => {
      await document.save();
    },
    { commandName: "Save PSD before embedding layout metadata" }
  );
}

module.exports = {
  getActiveDocument,
  getSingleSelectedLayer,
  readBounds,
  readText,
  saveIfDirty
};

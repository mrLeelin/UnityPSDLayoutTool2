const imaging = require("photoshop").imaging;

// UXP adapter. The inference service deliberately receives only this plain
// raster value so it remains independently testable without Photoshop.
async function readLayerRaster(document, layer) {
  if (!imaging || typeof imaging.getPixels !== "function") {
    throw new Error("This Photoshop version does not provide the UXP Imaging API required for auto 9-slice analysis.");
  }
  const bounds = layer.boundsNoEffects || layer.bounds;
  const left = Number(bounds.left);
  const top = Number(bounds.top);
  const right = Number.isFinite(Number(bounds.right)) ? Number(bounds.right) : left + Number(bounds.width);
  const bottom = Number.isFinite(Number(bounds.bottom)) ? Number(bounds.bottom) : top + Number(bounds.height);
  const sourceBounds = {
    left: Math.floor(left),
    top: Math.floor(top),
    right: Math.ceil(right),
    bottom: Math.ceil(bottom)
  };
  if (!Number.isFinite(sourceBounds.left) || !Number.isFinite(sourceBounds.top) ||
      sourceBounds.right <= sourceBounds.left || sourceBounds.bottom <= sourceBounds.top) {
    throw new Error("The selected layer has no valid pixel bounds for 9-slice analysis.");
  }
  const pixelResult = await imaging.getPixels({
    documentID: document.id,
    layerID: layer.id,
    sourceBounds
  });

  try {
    const imageData = pixelResult.imageData;
    const data = await imageData.getData({ chunky: true, fullRange: true });
    return {
      data,
      width: imageData.width,
      height: imageData.height,
      components: imageData.components
    };
  } finally {
    if (pixelResult.imageData) {
      pixelResult.imageData.dispose();
    }
  }
}

module.exports = { readLayerRaster };

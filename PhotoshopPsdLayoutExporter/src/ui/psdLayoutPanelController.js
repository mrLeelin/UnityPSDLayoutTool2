const { normalizeNineSlice, readLegacyNineSlice, validateNineSlice } = require("../nine-slice/nineSliceValue");
const { inferNineSliceFromRaster } = require("../nine-slice/nineSliceInferenceService");
const { buildManifest } = require("../metadata/psdLayoutManifestBuilder");
const xmpManifestStore = require("../metadata/psdXmpManifestStore");
const { readLayerRaster } = require("../photoshop/layerRasterReader");

function createPsdLayoutPanelController(documentGateway) {
  const statusElement = document.getElementById("status");
  const selectedLayerElement = document.getElementById("selected-layer");
  const nineSliceEnabledElement = document.getElementById("nine-slice-enabled");
  const nineSliceLeftElement = document.getElementById("nine-slice-left");
  const nineSliceTopElement = document.getElementById("nine-slice-top");
  const nineSliceRightElement = document.getElementById("nine-slice-right");
  const nineSliceBottomElement = document.getElementById("nine-slice-bottom");

  function connect() {
    document.getElementById("write-xmp").addEventListener("click", () => writeManifestToPsd());
    document.getElementById("apply-nine-slice").addEventListener("click", applyNineSliceToSelectedLayer);
    document.getElementById("auto-nine-slice").addEventListener("click", inferNineSliceForSelectedLayer);
    document.getElementById("load-nine-slice").addEventListener("click", loadNineSliceFromSelectedLayer);
    refreshSelectedLayer();
  }

  function setStatus(message, isError) {
    statusElement.textContent = message;
    statusElement.style.color = isError ? "#f88" : "#bbb";
  }

  function refreshSelectedLayer() {
    try {
      const document = documentGateway.getActiveDocument();
      const layer = documentGateway.getSingleSelectedLayer(document);
      const bounds = documentGateway.readBounds(layer.bounds);
      selectedLayerElement.textContent =
        `Selected: ${layer.name || "Untitled"}  |  ID: ${layer.id}  |  ${bounds.width} x ${bounds.height}px`;
    } catch (_) {
      selectedLayerElement.textContent = "Select exactly one layer.";
    }
  }

  async function applyNineSliceToSelectedLayer() {
    try {
      const document = documentGateway.getActiveDocument();
      const layer = documentGateway.getSingleSelectedLayer(document);
      const nineSlice = readNineSliceFromPanel();
      validateNineSlice(nineSlice, documentGateway.readBounds(layer.bounds));

      const overrides = new Map([[String(layer.id), nineSlice]]);
      await writeManifestToPsd(overrides, `Updated 9-slice for: ${layer.name || layer.id}`);
      refreshSelectedLayer();
    } catch (error) {
      setStatus(`9-slice update failed: ${error.message || error}`, true);
    }
  }

  async function loadNineSliceFromSelectedLayer() {
    try {
      const document = documentGateway.getActiveDocument();
      const layer = documentGateway.getSingleSelectedLayer(document);
      const stored = xmpManifestStore.collectStoredNineSlices(xmpManifestStore.readManifestForDocument(document));
      const nineSlice = stored.get(String(layer.id)) || readLegacyNineSlice(layer);
      writeNineSliceToPanel(nineSlice);
      setStatus(
        nineSlice && nineSlice.enabled
          ? `Loaded 9-slice settings for: ${layer.name || layer.id}`
          : `No enabled 9-slice settings for: ${layer.name || layer.id}`,
        false
      );
      refreshSelectedLayer();
    } catch (error) {
      setStatus(`Unable to load 9-slice settings: ${error.message || error}`, true);
    }
  }

  async function inferNineSliceForSelectedLayer() {
    try {
      const document = documentGateway.getActiveDocument();
      const layer = documentGateway.getSingleSelectedLayer(document);
      setStatus("Analyzing selected layer pixels for 9-slice…", false);
      const candidate = inferNineSliceFromRaster(await readLayerRaster(document, layer));
      writeNineSliceToPanel(candidate);
      setStatus(
        `Candidate ready (${candidate.inferMethod}, ${candidate.confidence} confidence). Review the values, then Apply to save.`,
        false
      );
    } catch (error) {
      setStatus(`9-slice analysis failed: ${error.message || error}`, true);
    }
  }

  async function writeManifestToPsd(overrides, successPrefix) {
    const document = documentGateway.getActiveDocument();
    if (!document.saved) {
      setStatus("Saving PSD before embedding layout metadata...", false);
      await documentGateway.saveIfDirty(document);
    }

    const nineSliceByLayerId = xmpManifestStore.collectStoredNineSlices(
      xmpManifestStore.readManifestForDocument(document)
    );
    if (overrides) {
      overrides.forEach((value, layerId) => {
        nineSliceByLayerId.set(String(layerId), normalizeNineSlice(value));
      });
    }

    const manifest = buildManifest(document, nineSliceByLayerId, documentGateway);
    xmpManifestStore.writeManifestForDocument(document, manifest);
    setStatus(
      `${successPrefix || "Wrote PSD embedded layout metadata"}\nLayers: ${manifest.layers.length}\nFingerprint: ${manifest.documentFingerprint}`,
      false
    );
  }

  function writeNineSliceToPanel(nineSlice) {
    const value = normalizeNineSlice(nineSlice);
    nineSliceEnabledElement.checked = value.enabled;
    nineSliceLeftElement.value = value.left || 0;
    nineSliceTopElement.value = value.top || 0;
    nineSliceRightElement.value = value.right || 0;
    nineSliceBottomElement.value = value.bottom || 0;
  }

  function readNineSliceFromPanel() {
    if (!nineSliceEnabledElement.checked) {
      return { enabled: false };
    }

    return {
      enabled: true,
      left: readNonNegativeNumber(nineSliceLeftElement, "Left"),
      top: readNonNegativeNumber(nineSliceTopElement, "Top"),
      right: readNonNegativeNumber(nineSliceRightElement, "Right"),
      bottom: readNonNegativeNumber(nineSliceBottomElement, "Bottom")
    };
  }

  function readNonNegativeNumber(element, label) {
    const value = Number(element.value);
    if (!Number.isFinite(value) || value < 0) {
      throw new Error(`${label} must be a non-negative number.`);
    }
    return value;
  }

  return { connect };
}

module.exports = { createPsdLayoutPanelController };

const { numberOr, stringOr } = require("../shared/values");

function normalizeNineSlice(value) {
  if (!value || !value.enabled) {
    return { enabled: false };
  }

  return {
    enabled: true,
    left: numberOr(Number(value.left), 0),
    top: numberOr(Number(value.top), 0),
    right: numberOr(Number(value.right), 0),
    bottom: numberOr(Number(value.bottom), 0)
  };
}

function readLegacyNineSlice(layer) {
  const name = stringOr(layer && layer.name, "");
  const match = name.match(
    /(?:\|9slice\s*=\s*|\[9slice\s*:\s*)([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\]?/i
  );
  if (!match) {
    return null;
  }

  return normalizeNineSlice({
    enabled: true,
    left: Number(match[1]),
    top: Number(match[2]),
    right: Number(match[3]),
    bottom: Number(match[4])
  });
}

function validateNineSlice(nineSlice, bounds) {
  if (!nineSlice.enabled) {
    return;
  }

  if (bounds.width <= 0 || bounds.height <= 0) {
    throw new Error("The selected layer has no exportable pixel bounds.");
  }
  if (nineSlice.left + nineSlice.right > bounds.width ||
      nineSlice.top + nineSlice.bottom > bounds.height) {
    throw new Error("The horizontal or vertical border sum is larger than the selected layer.");
  }
}

module.exports = {
  normalizeNineSlice,
  readLegacyNineSlice,
  validateNineSlice
};

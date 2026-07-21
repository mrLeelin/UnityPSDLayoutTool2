const MIN_BORDER = 4;
const MAX_BORDER_RATIO = 0.45;
const EDGE_SAMPLE_RATIO = 0.60;
const EDGE_STABLE_RUN = 3;
const EDGE_DIFF_THRESHOLD = 12;
const MERGE_REPEAT_MULTIPLIER = 1.5;
const MERGE_MAX_RATIO = 0.20;
const MERGE_MAX_PIXELS = 96;
const ROUND_RECT_MIN_INSET_RATIO = 0.12;
const ROUND_RECT_SAFETY_MULTIPLIER = 1.25;
const ROUND_RECT_MIN_CENTER = 20;

// Pure domain service. It knows nothing about UXP, Photoshop documents, XMP,
// or panel controls; the Photoshop adapter supplies a flattened RGBA raster.
function inferNineSliceFromRaster(raster) {
  validateRaster(raster);
  if (!hasVisiblePixels(raster)) {
    throw new Error("The selected layer has no visible pixels for 9-slice analysis.");
  }

  const visual = inferVisualEdgeBorders(raster);
  const repeat = inferRepeatBorders(raster);
  const border = mergeBorders(visual, repeat, raster.width, raster.height);
  const rounded = inferRoundRectBorder(raster);
  const protectedBorder = rounded ? applyRoundRectProtection(border, rounded, raster) : border;

  return {
    enabled: true,
    left: protectedBorder.left,
    top: protectedBorder.top,
    right: protectedBorder.right,
    bottom: protectedBorder.bottom,
    confidence: "medium",
    inferMethod: rounded ? "visual-repeat-merged-round-rect" : "visual-repeat-merged",
    warnings: ["Automatically inferred from layer pixels; verify the border before applying."]
  };
}

function validateRaster(raster) {
  if (!raster || !raster.data || raster.width < 8 || raster.height < 8) {
    throw new Error("9-slice analysis requires a raster at least 8 x 8 pixels.");
  }
  if (raster.components < 1 || raster.components > 4 ||
      raster.data.length < raster.width * raster.height * raster.components) {
    throw new Error("The selected layer returned invalid pixel data.");
  }
}

function hasVisiblePixels(raster) {
  const alphaIndex = raster.components === 4 ? 3 : raster.components === 2 ? 1 : -1;
  if (alphaIndex < 0) {
    return true;
  }
  for (let index = alphaIndex; index < raster.data.length; index += raster.components) {
    if (raster.data[index] > 0) {
      return true;
    }
  }
  return false;
}

function inferVisualEdgeBorders(raster) {
  const xMargin = Math.floor(raster.width * (1 - EDGE_SAMPLE_RATIO) * 0.5);
  const yMargin = Math.floor(raster.height * (1 - EDGE_SAMPLE_RATIO) * 0.5);
  const x0 = Math.max(0, xMargin);
  const x1 = Math.min(raster.width, raster.width - xMargin);
  const y0 = Math.max(0, yMargin);
  const y1 = Math.min(raster.height, raster.height - yMargin);
  const rows = Array.from({ length: raster.height }, (_, y) => averageRow(raster, y, x0, x1));
  const columns = Array.from({ length: raster.width }, (_, x) => averageColumn(raster, x, y0, y1));

  return {
    left: findVisualEdgeBorder(columns, averageRgba(columns.slice(x0, x1)), true),
    right: findVisualEdgeBorder(columns, averageRgba(columns.slice(x0, x1)), false),
    top: findVisualEdgeBorder(rows, averageRgba(rows.slice(y0, y1)), true),
    bottom: findVisualEdgeBorder(rows, averageRgba(rows.slice(y0, y1)), false)
  };
}

function inferRepeatBorders(raster) {
  return {
    left: findRepeatBorder(raster, true, true),
    right: findRepeatBorder(raster, true, false),
    top: findRepeatBorder(raster, false, true),
    bottom: findRepeatBorder(raster, false, false)
  };
}

function findRepeatBorder(raster, scanColumns, fromStart) {
  const size = scanColumns ? raster.width : raster.height;
  const maxBorder = Math.floor(size * MAX_BORDER_RATIO);
  let previous = null;
  let repeatCount = 0;
  for (let offset = 0; offset < maxBorder; offset += 1) {
    const index = fromStart ? offset : size - 1 - offset;
    if (previous !== null && lineEquals(raster, scanColumns, index, previous)) {
      repeatCount += 1;
      if (repeatCount >= 2) {
        return Math.max(offset - repeatCount, MIN_BORDER);
      }
    } else {
      repeatCount = 0;
    }
    previous = index;
  }
  return MIN_BORDER;
}

function lineEquals(raster, scanColumns, first, second) {
  const length = scanColumns ? raster.height : raster.width;
  for (let position = 0; position < length; position += 1) {
    for (let component = 0; component < raster.components; component += 1) {
      if (componentAt(raster, scanColumns ? first : position, scanColumns ? position : first, component) !==
          componentAt(raster, scanColumns ? second : position, scanColumns ? position : second, component)) {
        return false;
      }
    }
  }
  return true;
}

function findVisualEdgeBorder(samples, baseline, fromStart) {
  const maxBorder = Math.max(1, Math.floor(samples.length * MAX_BORDER_RATIO));
  let stableRun = 0;
  for (let offset = 0; offset < maxBorder; offset += 1) {
    const index = fromStart ? offset : samples.length - 1 - offset;
    if (rgbaDiff(samples[index], baseline) <= EDGE_DIFF_THRESHOLD) {
      stableRun += 1;
      if (stableRun >= EDGE_STABLE_RUN) {
        return Math.max(offset - stableRun + 1, MIN_BORDER);
      }
    } else {
      stableRun = 0;
    }
  }
  return MIN_BORDER;
}

function mergeBorders(visual, repeat, width, height) {
  return {
    left: mergeBorderValue(visual.left, repeat.left, width),
    right: mergeBorderValue(visual.right, repeat.right, width),
    top: mergeBorderValue(visual.top, repeat.top, height),
    bottom: mergeBorderValue(visual.bottom, repeat.bottom, height)
  };
}

function mergeBorderValue(visual, repeat, axisSize) {
  let base;
  if (repeat <= 0) {
    base = visual;
  } else if (repeat <= MIN_BORDER && visual > repeat * MERGE_REPEAT_MULTIPLIER) {
    base = visual;
  } else if (visual <= repeat * MERGE_REPEAT_MULTIPLIER) {
    base = visual;
  } else {
    base = Math.max(repeat, Math.min(visual, repeat * MERGE_REPEAT_MULTIPLIER));
  }
  return Math.max(MIN_BORDER, Math.min(base, Math.min(axisSize * MERGE_MAX_RATIO, MERGE_MAX_PIXELS)));
}

function inferRoundRectBorder(raster) {
  const insets = [
    alphaInsetOnRow(raster, 0, true), alphaInsetOnRow(raster, 0, false),
    alphaInsetOnRow(raster, raster.height - 1, true), alphaInsetOnRow(raster, raster.height - 1, false),
    alphaInsetOnColumn(raster, 0, true), alphaInsetOnColumn(raster, 0, false),
    alphaInsetOnColumn(raster, raster.width - 1, true), alphaInsetOnColumn(raster, raster.width - 1, false)
  ].filter(value => value !== null);
  if (!insets.length) {
    return null;
  }
  const shortAxis = Math.min(raster.width, raster.height);
  const maxInset = Math.max(...insets);
  if (maxInset < shortAxis * ROUND_RECT_MIN_INSET_RATIO) {
    return null;
  }

  const maxForVisual = Math.max(MIN_BORDER, Math.floor(shortAxis * MAX_BORDER_RATIO));
  const maxForCenter = Math.floor((shortAxis - ROUND_RECT_MIN_CENTER) * 0.5);
  const maxBorder = maxForCenter >= MIN_BORDER ? Math.min(maxForVisual, maxForCenter) : maxForVisual;
  const border = Math.max(MIN_BORDER, Math.min(Math.ceil(maxInset * ROUND_RECT_SAFETY_MULTIPLIER), maxBorder));
  return border * 2 < raster.width && border * 2 < raster.height ? border : null;
}

function applyRoundRectProtection(border, roundedBorder, raster) {
  return {
    left: Math.min(Math.max(border.left, roundedBorder), Math.floor((raster.width - 1) / 2)),
    right: Math.min(Math.max(border.right, roundedBorder), Math.floor((raster.width - 1) / 2)),
    top: Math.min(Math.max(border.top, roundedBorder), Math.floor((raster.height - 1) / 2)),
    bottom: Math.min(Math.max(border.bottom, roundedBorder), Math.floor((raster.height - 1) / 2))
  };
}

function alphaInsetOnRow(raster, y, fromStart) {
  for (let offset = 0; offset < raster.width; offset += 1) {
    const x = fromStart ? offset : raster.width - 1 - offset;
    if (alphaAt(raster, x, y) > 0) {
      return offset;
    }
  }
  return null;
}

function alphaInsetOnColumn(raster, x, fromStart) {
  for (let offset = 0; offset < raster.height; offset += 1) {
    const y = fromStart ? offset : raster.height - 1 - offset;
    if (alphaAt(raster, x, y) > 0) {
      return offset;
    }
  }
  return null;
}

function averageRow(raster, y, x0, x1) {
  const values = [];
  for (let x = x0; x < Math.max(x0 + 1, x1); x += 1) {
    values.push(rgbaAt(raster, x, y));
  }
  return averageRgba(values);
}

function averageColumn(raster, x, y0, y1) {
  const values = [];
  for (let y = y0; y < Math.max(y0 + 1, y1); y += 1) {
    values.push(rgbaAt(raster, x, y));
  }
  return averageRgba(values);
}

function averageRgba(values) {
  if (!values.length) {
    return [0, 0, 0, 0];
  }
  return values.reduce((total, value) => [
    total[0] + value[0], total[1] + value[1], total[2] + value[2], total[3] + value[3]
  ], [0, 0, 0, 0]).map(value => value / values.length);
}

function rgbaDiff(first, second) {
  const rgbDifference = (Math.abs(first[0] - second[0]) + Math.abs(first[1] - second[1]) + Math.abs(first[2] - second[2])) / 3;
  return Math.max(rgbDifference, Math.abs(first[3] - second[3]));
}

function rgbaAt(raster, x, y) {
  const first = componentAt(raster, x, y, 0);
  if (raster.components === 1) return [first, first, first, 255];
  if (raster.components === 2) return [first, first, first, componentAt(raster, x, y, 1)];
  if (raster.components === 3) return [first, componentAt(raster, x, y, 1), componentAt(raster, x, y, 2), 255];
  return [first, componentAt(raster, x, y, 1), componentAt(raster, x, y, 2), componentAt(raster, x, y, 3)];
}

function alphaAt(raster, x, y) {
  return rgbaAt(raster, x, y)[3];
}

function componentAt(raster, x, y, component) {
  return raster.data[(y * raster.width + x) * raster.components + component];
}

module.exports = { inferNineSliceFromRaster };

function numberOr(value, fallback) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringOr(value, fallback) {
  return typeof value === "string" ? value : fallback;
}

function stableKeys(value) {
  return Object.keys(value).sort();
}

function hashString(value) {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(16).padStart(8, "0");
}

function toBase64Utf8(value) {
  return btoa(unescape(encodeURIComponent(value)));
}

function fromBase64Utf8(value) {
  return decodeURIComponent(escape(atob(value)));
}

module.exports = {
  fromBase64Utf8,
  hashString,
  numberOr,
  stableKeys,
  stringOr,
  toBase64Utf8
};

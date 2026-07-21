namespace PhotoshopFile
{
    using System;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// The versioned source-state manifest written by the Photoshop companion
    /// plugin into the PSD XMP image resource.
    /// </summary>
    [Serializable]
    public sealed class PsdEmbeddedLayoutManifest
    {
        public string schema;
        public int schemaVersion;
        public int nineSliceSchemaVersion;
        public string source;
        public string documentFingerprint;
        public PsdEmbeddedLayoutDocument document;
        public PsdEmbeddedLayoutLayer[] layers;

        /// <summary>Gets whether the manifest is structurally usable.</summary>
        public bool IsUsable
        {
            get
            {
                return string.Equals(schema, "psd-unity-layout", StringComparison.Ordinal) &&
                    schemaVersion == 1 &&
                    !string.IsNullOrEmpty(documentFingerprint) &&
                    layers != null;
            }
        }

        /// <summary>Decodes the compact UTF-8 Base64 value stored in XMP.</summary>
        public static PsdEmbeddedLayoutManifest FromBase64Utf8(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                string json = Encoding.UTF8.GetString(bytes);
                PsdEmbeddedLayoutManifest manifest = JsonUtility.FromJson<PsdEmbeddedLayoutManifest>(json);
                return manifest != null && manifest.IsUsable ? manifest : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PSD embedded layout manifest could not be decoded: " + exception.Message);
                return null;
            }
        }
    }

    [Serializable]
    public sealed class PsdEmbeddedLayoutDocument
    {
        public string name;
        public int width;
        public int height;
        public float resolution;
    }

    [Serializable]
    public sealed class PsdEmbeddedLayoutLayer
    {
        public string layerId;
        public string parentId;
        public int siblingIndex;
        public int[] path;
        public string name;
        public string kind;
        public bool visible;
        public float opacity;
        public PsdEmbeddedLayoutBounds bounds;
        public PsdEmbeddedLayoutText text;
        public PsdEmbeddedLayoutNineSlice nineSlice;
        public string fingerprint;
    }

    [Serializable]
    public sealed class PsdEmbeddedLayoutBounds
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class PsdEmbeddedLayoutText
    {
        public string contents;
        public bool isPointText;
        public bool isParagraphText;
        public float fontSize;
        public string fontName;
        public string justification;
    }

    [Serializable]
    public sealed class PsdEmbeddedLayoutNineSlice
    {
        public bool enabled;
        public float left;
        public float top;
        public float right;
        public float bottom;
    }
}

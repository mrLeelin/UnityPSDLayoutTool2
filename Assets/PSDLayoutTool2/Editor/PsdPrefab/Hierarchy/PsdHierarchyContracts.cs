namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Central resource limits for both sides of the AI trust boundary. Values
    /// comfortably cover the current 109-node production fixture while keeping
    /// hostile JSON and Prefab metadata from causing unbounded allocations.
    /// </summary>
    public static class PsdHierarchyContractLimits
    {
        public const int MaxJsonCharacters = 2000000;
        public const int MaxJsonUtf8Bytes = 3500000;
        public const int MaxGroups = 512;
        public const int MaxRenames = 2048;
        public const int MaxMembersPerGroup = 512;
        public const int MaxTotalMemberships = 16384;
        public const int MaxContextNodes = 4096;
        public const int MaxPrefabMetadataNodes = 8192;
        public const int MaxComponentTypesPerNode = 64;
        public const int MaxTotalComponentTypes = 65536;
        public const int MaxPreviews = 128;
        public const int MaxIdentifierLength = 256;
        public const int MaxNameLength = 512;
        public const int MaxEvidenceLength = 2048;
        public const int MaxFingerprintLength = 128;
        public const int MaxHierarchyPathLength = 1024;
        public const int MaxPreviewKindLength = 128;
        public const int MaxSourceGuidLength = 128;
        public const int MaxDocumentDimension = 100000;
        public const float MaxCoordinateMagnitude = 10000000f;
    }

    /// <summary>
    /// Read-only, bounded description sent to a hierarchy planner. It contains
    /// layout facts and identifiers only; it deliberately has no texture bytes,
    /// filesystem command, or Unity write-operation field.
    /// </summary>
    [Serializable]
    public sealed class PsdHierarchyRequest
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string sourcePsdGuid;
        public string sourceFingerprint;
        public string contentFingerprint;
        public string structureFingerprint;
        public string geometryFingerprint;
        public int documentWidth;
        public int documentHeight;
        public List<PsdHierarchyRequestNode> nodes = new List<PsdHierarchyRequestNode>();
        public List<PsdHierarchyPrefabNodeMetadata> currentPrefabHierarchy = new List<PsdHierarchyPrefabNodeMetadata>();
        public List<PsdHierarchyPreviewReference> previews = new List<PsdHierarchyPreviewReference>();
    }

    [Serializable]
    public sealed class PsdHierarchyRequestNode
    {
        public string stableId;
        public string originalName;
        public string kind;
        public string parentStableId;
        public int siblingIndex;
        public PsdHierarchyRectangle rectangle;
        public bool hasProjectComponents;
        public bool isProtectedBoundary;
        public string protectedBoundaryStableId;
    }

    /// <summary>
    /// A serializable rectangle independent of Unity object references. This is
    /// intentionally just geometry, not a Transform mutation instruction.
    /// </summary>
    [Serializable]
    public struct PsdHierarchyRectangle
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    /// <summary>
    /// Current Prefab facts used to prevent a plan from crossing project-owned
    /// components or protected hierarchy boundaries.
    /// </summary>
    [Serializable]
    public sealed class PsdHierarchyPrefabNodeMetadata
    {
        public string stableId;
        public string parentStableId;
        public int siblingIndex;
        public string hierarchyPath;
        public List<string> componentTypes = new List<string>();
        public bool hasProjectComponents;
        public bool isProtectedBoundary;
        public string protectedBoundaryStableId;
    }

    /// <summary>
    /// Optional reference to a separately prepared preview or crop. The payload
    /// is never embedded here, keeping the core request small and auditable.
    /// </summary>
    [Serializable]
    public sealed class PsdHierarchyPreviewReference
    {
        public string key;
        public string kind;
        public PsdHierarchyRectangle crop;
    }

    [Serializable]
    public sealed class PsdHierarchyPlan
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion;
        public string sourcePsdGuid;
        public string sourceFingerprint;
        public string contentFingerprint;
        public string structureFingerprint;
        public string geometryFingerprint;
        public List<PsdHierarchyPlanGroup> groups = new List<PsdHierarchyPlanGroup>();
        public List<PsdHierarchyPlanRename> renames = new List<PsdHierarchyPlanRename>();
    }

    [Serializable]
    public sealed class PsdHierarchyPlanGroup
    {
        public string key;
        public string parentKey;
        public List<string> memberStableIds = new List<string>();
        public string displayName;
        public string evidence;
        public double confidence;
    }

    [Serializable]
    public sealed class PsdHierarchyPlanRename
    {
        public string stableId;
        public string name;
        public string evidence;
        public double confidence;
    }

    /// <summary>
    /// Separates harmless content drift from topology and geometry changes.
    /// Geometry drift is not automatically invalid, but it must pass the later
    /// geometry validation stage before a plan can be directly applied.
    /// </summary>
    public enum PsdHierarchyPlanFingerprintStatus
    {
        Valid,
        RequiresGeometryValidation,
        RequiresReplan
    }

    public sealed class PsdHierarchyPlanFormatException : FormatException
    {
        public PsdHierarchyPlanFormatException(string message)
            : base(message)
        {
        }

        public PsdHierarchyPlanFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class PsdHierarchyPlanValidationException : InvalidOperationException
    {
        public PsdHierarchyPlanValidationException(string message)
            : base(message)
        {
        }
    }
}

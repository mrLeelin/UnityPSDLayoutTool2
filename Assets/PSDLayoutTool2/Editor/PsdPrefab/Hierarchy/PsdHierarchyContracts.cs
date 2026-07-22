namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

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
        public string sourceFingerprint;
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
        public string sourceFingerprint;
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

namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter), true)]
    internal enum PsdHierarchyWebOperationKind
    {
        None,
        Analyze,
        Refine,
        Accept,
        Apply,
        CreatePrefabs
    }

    [JsonConverter(typeof(StringEnumConverter), true)]
    internal enum PsdHierarchyWebOperationStatus
    {
        Idle,
        Running,
        Succeeded,
        Failed
    }

    [Serializable]
    internal sealed class PsdHierarchyWebSessionDto
    {
        public string sessionId = string.Empty;
        public string sourcePsdName = string.Empty;
        public string targetPrefabName = string.Empty;
        public bool canAnalyze = false;
        public bool canApply = false;
        public bool canCreatePrefabs = false;
        public PsdHierarchyWebOperationState operation = new PsdHierarchyWebOperationState();
    }

    [Serializable]
    internal sealed class PsdHierarchyWebSnapshotDto
    {
        public PsdHierarchyWebBoundsDto canvas = new PsdHierarchyWebBoundsDto();
        public List<PsdHierarchyWebNodeDto> nodes = new List<PsdHierarchyWebNodeDto>();
        public List<PsdHierarchyWebGroupDto> groups = new List<PsdHierarchyWebGroupDto>();
        public List<PsdHierarchyWebWarningDto> warnings = new List<PsdHierarchyWebWarningDto>();
        public List<PsdHierarchyWebPrefabCandidateDto> prefabCandidates =
            new List<PsdHierarchyWebPrefabCandidateDto>();
    }

    [Serializable]
    internal sealed class PsdHierarchyWebNodeDto
    {
        public string stableId = string.Empty;
        public string parentStableId = string.Empty;
        public string name = string.Empty;
        public string proposedName = string.Empty;
        public string kind = string.Empty;
        public PsdHierarchyWebBoundsDto bounds = new PsdHierarchyWebBoundsDto();
        public string sourceGroupKey = string.Empty;
        public string proposedGroupKey = string.Empty;
        public bool isAccepted = false;
        public bool isLocked = false;
        public bool hasWarning = false;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebGroupDto
    {
        public string key = string.Empty;
        public string parentKey = string.Empty;
        public string displayName = string.Empty;
        public List<string> memberStableIds = new List<string>();
        public PsdHierarchyWebBoundsDto bounds = new PsdHierarchyWebBoundsDto();
        public bool isAccepted = false;
        public bool isLocked = false;
        public string evidence = string.Empty;
        public double confidence = 0d;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebBoundsDto
    {
        public float x = 0f;
        public float y = 0f;
        public float width = 0f;
        public float height = 0f;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebWarningDto
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public List<string> stableIds = new List<string>();
    }

    [Serializable]
    internal sealed class PsdHierarchyWebPrefabCandidateDto
    {
        public string candidateId = string.Empty;
        public string proposedName = string.Empty;
        public string representativeStableId = string.Empty;
        public List<string> instanceStableIds = new List<string>();
        public List<string> instanceControlledDifferences = new List<string>();
    }

    [Serializable]
    internal sealed class PsdHierarchyWebRefineRequest
    {
        public List<string> stableIds = new List<string>();
        public string instruction = string.Empty;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebAcceptRequest
    {
        public List<string> groupKeys = new List<string>();
        public bool isAccepted = true;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebApplyRequest
    {
        public bool confirmed = false;
    }

    [Serializable]
    internal sealed class PsdHierarchyWebCreatePrefabsRequest
    {
        public List<string> candidateIds = new List<string>();
    }

    [Serializable]
    internal sealed class PsdHierarchyWebOperationState
    {
        public string operationId = string.Empty;
        public PsdHierarchyWebOperationKind kind = PsdHierarchyWebOperationKind.None;
        public PsdHierarchyWebOperationStatus status = PsdHierarchyWebOperationStatus.Idle;
        public string message = string.Empty;
    }
}

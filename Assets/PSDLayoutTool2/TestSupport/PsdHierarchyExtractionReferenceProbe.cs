namespace PsdLayoutTool2.Tests
{
    using UnityEngine;
    using UnityEngine.Events;

    public sealed class PsdHierarchyExtractionReferenceProbe : MonoBehaviour
    {
        public GameObject[] targets;
        public PsdHierarchyExtractionNestedValues nested = new PsdHierarchyExtractionNestedValues();
        [HideInInspector] public GameObject hiddenTarget;
        public UnityEvent onInvoke = new UnityEvent();
    }
}

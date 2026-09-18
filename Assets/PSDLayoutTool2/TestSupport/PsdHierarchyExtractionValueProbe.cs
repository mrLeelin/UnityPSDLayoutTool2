namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Events;

    [Serializable]
    public sealed class PsdHierarchyExtractionNestedValues
    {
        public string label;
        public GameObject target;
        [HideInInspector] public GameObject hiddenTarget;
    }

    public sealed class PsdHierarchyExtractionValueProbe : MonoBehaviour
    {
        public PsdHierarchyExtractionNestedValues nested = new PsdHierarchyExtractionNestedValues();
        public List<GameObject> targets = new List<GameObject>();
        public UnityEvent onInvoke = new UnityEvent();

        public void HandleInvoke() { }
    }
}

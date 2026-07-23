namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Resolves Profile paths against the objects in the currently opened Prefab
    /// Stage. A path must have exactly one Stage match; ambiguity is rejected.
    /// </summary>
    internal static class PsdHierarchyPrefabStageSelection
    {
        internal static IReadOnlyList<T> ResolveStageTargets<T>(
            IEnumerable<string> requestedPaths,
            IReadOnlyList<string> stagePaths,
            IReadOnlyList<T> stageObjects,
            StringComparer comparer)
        {
            if (requestedPaths == null) throw new ArgumentNullException("requestedPaths");
            if (stagePaths == null) throw new ArgumentNullException("stagePaths");
            if (stageObjects == null) throw new ArgumentNullException("stageObjects");
            if (comparer == null) throw new ArgumentNullException("comparer");
            if (stagePaths.Count != stageObjects.Count)
                throw new InvalidOperationException("Prefab Stage path/object counts do not match.");

            var result = new List<T>();
            foreach (string requestedPath in requestedPaths)
            {
                if (string.IsNullOrEmpty(requestedPath))
                    throw new InvalidOperationException("Profile contains an empty hierarchy path.");
                int match = -1;
                for (int index = 0; index < stagePaths.Count; index++)
                {
                    if (!comparer.Equals(requestedPath, stagePaths[index])) continue;
                    if (match >= 0)
                        throw new InvalidOperationException("Prefab Stage hierarchy path is ambiguous: " + requestedPath);
                    match = index;
                }
                if (match < 0)
                    throw new InvalidOperationException("Prefab Stage object is missing: " + requestedPath);
                result.Add(stageObjects[match]);
            }
            return result;
        }
    }
}

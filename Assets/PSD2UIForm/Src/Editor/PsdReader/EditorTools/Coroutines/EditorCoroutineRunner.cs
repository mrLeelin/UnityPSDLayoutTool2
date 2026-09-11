using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using EditorCoroutineTaskNamespace;

namespace cn.efunstudio.psdreader
{
    internal class EditorCoroutineRunner
    {
        private static List<EditorCoroutineTask> coroutineStates;

        private static List<EditorCoroutineTask> finishedThisUpdate;

        private static EditorCoroutineTask uiCoroutineState;

        internal static EditorCoroutineRunner s_ObfuscationSentinel;

        public static void KillAllCoroutines()
        {
            EditorUtility.ClearProgressBar();
            uiCoroutineState = null;
            coroutineStates.Clear();
            finishedThisUpdate.Clear();
        }

        public static EditorCoroutine StartCoroutine(IEnumerator coroutine)
        {
            return StoreCoroutine(new EditorCoroutineTask(coroutine));
        }

        public static EditorCoroutine StartCoroutineWithUI(IEnumerator coroutine, string title, bool isCancelable = false)
        {
            if (uiCoroutineState == null)
            {
                uiCoroutineState = new EditorCoroutineTask(coroutine, title, isCancelable);
                return StoreCoroutine(uiCoroutineState);
            }
            Debug.LogError((object)("EditorCoroutineRunner only supports running one coroutine that draws a GUI! [" + title + "]"));
            return null;
        }

        private static EditorCoroutine StoreCoroutine(EditorCoroutineTask state)
        {
            if (coroutineStates == null)
            {
                coroutineStates = new List<EditorCoroutineTask>();
                finishedThisUpdate = new List<EditorCoroutineTask>();
            }
            if (coroutineStates.Count == 0)
            {
                EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(Runner));
            }
            coroutineStates.Add(state);
            return state.EditorCoroutineYieldInstruction;
        }

        public static void UpdateUILabel(string label)
        {
            if (uiCoroutineState != null && uiCoroutineState.ShowUI)
            {
                uiCoroutineState.Label = label;
            }
        }

        public static void UpdateUIProgressBar(float percent)
        {
            if (uiCoroutineState != null && uiCoroutineState.ShowUI)
            {
                uiCoroutineState.PercentComplete = percent;
            }
        }

        public static void UpdateUI(string label, float percent)
        {
            if (uiCoroutineState != null && uiCoroutineState.ShowUI)
            {
                uiCoroutineState.Label = label;
                uiCoroutineState.PercentComplete = percent;
            }
        }

        private static void Runner()
        {
            for (int i = 0; i < coroutineStates.Count; i++)
            {
                TickState(coroutineStates[i]);
            }
            for (int j = 0; j < finishedThisUpdate.Count; j++)
            {
                coroutineStates.Remove(finishedThisUpdate[j]);
                if (uiCoroutineState == finishedThisUpdate[j])
                {
                    uiCoroutineState = null;
                    EditorUtility.ClearProgressBar();
                }
            }
            finishedThisUpdate.Clear();
            if (coroutineStates.Count == 0)
            {
                EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(Runner));
            }
        }

        private static void TickState(EditorCoroutineTask state)
        {
            if (!state.IsValid())
            {
                finishedThisUpdate.Add(state);
                return;
            }
            state.Tick();
            if (state.ShowUI && uiCoroutineState == state)
            {
                uiCoroutineState.UpdateUI();
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EditorCoroutineRunner GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

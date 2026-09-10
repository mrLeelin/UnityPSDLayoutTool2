using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;

namespace EditorCoroutineTaskNamespace
{
    internal class EditorCoroutineTask
    {
        private IEnumerator _coroutine;

        public EditorCoroutine EditorCoroutineYieldInstruction;

        private object _current;

        private Type _currentType;

        private float _timer;

        private EditorCoroutine _nestedCoroutine;

        private DateTime _lastUpdateTime;

        public bool ShowUI;

        private bool _isCancelable;

        private bool _isCanceled;

        private string _title;

        public string Label;

        public float PercentComplete;

        private static EditorCoroutineTask s_ObfuscationSentinel;

        [SpecialName]
        public bool IsValid()
        {
            return _coroutine != null;
        }

        public EditorCoroutineTask(IEnumerator enumerator)
        {
            _coroutine = enumerator;
            EditorCoroutineYieldInstruction = new EditorCoroutine();
            ShowUI = false;
            _lastUpdateTime = DateTime.Now;
        }

        public EditorCoroutineTask(IEnumerator enumerator, string text, bool enabled)
        {
            _coroutine = enumerator;
            EditorCoroutineYieldInstruction = new EditorCoroutine();
            ShowUI = true;
            _isCancelable = enabled;
            _title = text;
            Label = "initializing....";
            PercentComplete = 0f;
            _lastUpdateTime = DateTime.Now;
        }

        public void Tick()
        {
            if (_coroutine == null)
            {
                return;
            }
            if (_isCanceled)
            {
                Stop();
                return;
            }
            bool flag = false;
            DateTime now = DateTime.Now;
            if (_current != null)
            {
                if (!(_currentType == typeof(WaitForSeconds)))
                {
                    if (_currentType == typeof(WaitForEndOfFrame) || _currentType == typeof(WaitForFixedUpdate))
                    {
                        flag = false;
                    }
                    else if (!typeof(AsyncOperation).IsAssignableFrom(_currentType))
                    {
                        if (!_currentType.IsSubclassOf(typeof(CustomYieldInstruction)))
                        {
                            if (!(_currentType == typeof(EditorCoroutine)))
                            {
                                if (typeof(IEnumerator).IsAssignableFrom(_currentType))
                                {
                                    if (_nestedCoroutine != null)
                                    {
                                        flag = !_nestedCoroutine.HasFinished;
                                    }
                                    else
                                    {
                                        _nestedCoroutine = EditorCoroutineRunner.StartCoroutine(_current as IEnumerator);
                                        flag = true;
                                    }
                                }
                                else if (!(_currentType == typeof(Coroutine)))
                                {
                                    Debug.LogError((object)("Unsupported yield (" + _currentType?.ToString() + ") in editor coroutine!! Canceling."));
                                    _isCanceled = true;
                                }
                                else
                                {
                                    Debug.LogError((object)"Nested Coroutines started by Unity's defaut StartCoroutine method are not supported in editor! please use EditorCoroutineRunner.Start instead. Canceling.");
                                    _isCanceled = true;
                                }
                            }
                            else if (!(_current as EditorCoroutine).HasFinished)
                            {
                                flag = true;
                            }
                        }
                        else
                        {
                            object obj = _current;
                            if (((CustomYieldInstruction)((obj is CustomYieldInstruction) ? obj : null)).keepWaiting)
                            {
                                flag = true;
                            }
                        }
                    }
                    else
                    {
                        object obj2 = _current;
                        AsyncOperation val = (AsyncOperation)((obj2 is AsyncOperation) ? obj2 : null);
                        if (val != null && !val.isDone)
                        {
                            flag = true;
                        }
                    }
                }
                else
                {
                    _timer -= (float)(now - _lastUpdateTime).TotalSeconds;
                    if (_timer > 0f)
                    {
                        flag = true;
                    }
                }
            }
            _lastUpdateTime = now;
            if (!_isCanceled)
            {
                if (flag)
                {
                    return;
                }
                if (_coroutine.MoveNext())
                {
                    _current = _coroutine.Current;
                    if (_current == null)
                    {
                        return;
                    }
                    _currentType = _current.GetType();
                    if (!(_currentType == typeof(WaitForSeconds)))
                    {
                        if (_currentType == typeof(EditorStatusUpdate))
                        {
                            EditorStatusUpdate editorStatusUpdate = _current as EditorStatusUpdate;
                            if (editorStatusUpdate.HasLabelUpdate)
                            {
                                Label = editorStatusUpdate.Label;
                            }
                            if (editorStatusUpdate.HasPercentUpdate)
                            {
                                PercentComplete = editorStatusUpdate.PercentComplete;
                            }
                        }
                    }
                    else
                    {
                        object obj3 = _current;
                        WaitForSeconds obj4 = (WaitForSeconds)((obj3 is WaitForSeconds) ? obj3 : null);
                        FieldInfo field = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            _timer = (float)field.GetValue(obj4);
                        }
                    }
                }
                else
                {
                    Stop();
                }
            }
            else
            {
                Stop();
            }
        }

        private void Stop()
        {
            _coroutine = null;
            EditorCoroutineYieldInstruction.HasFinished = true;
        }

        public void UpdateUI()
        {
            if (!_isCancelable)
            {
                EditorUtility.DisplayProgressBar(_title, Label, PercentComplete);
                return;
            }
            _isCanceled = EditorUtility.DisplayCancelableProgressBar(_title, Label, PercentComplete);
            if (_isCanceled)
            {
                Debug.Log((object)"CANCELED");
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EditorCoroutineTask GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

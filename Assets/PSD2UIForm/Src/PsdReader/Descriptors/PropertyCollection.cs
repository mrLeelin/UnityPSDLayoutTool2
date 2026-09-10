using System;
using System.Collections;
using System.Collections.Generic;
using cn.efunstudio.psdreader.PsdParser;

namespace PropertyCollectionNamespace
{
    internal class PropertyCollection : IEnumerable<KeyValuePair<string, object>>, IEnumerable, IProperties
    {
        private readonly Dictionary<string, object> _values;

        internal static PropertyCollection s_ObfuscationSentinel;

        public int Count => _values.Count;

        public object this[string property]
        {
            get
            {
                return GetValue(property);
            }
            set
            {
                _values[property] = value;
            }
        }

        public PropertyCollection()
        {
            _values = new Dictionary<string, object>();
        }

        public PropertyCollection(int value)
        {
            _values = new Dictionary<string, object>(value);
        }

        public void Add(string key, object value)
        {
            _values.Add(key, value);
        }

        public bool Contains(string text2)
        {
            string[] array = text2.Split(new char[3] { '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            object obj = _values;
            string[] array2 = array;
            foreach (string text in array2)
            {
                if (!(obj is ArrayList))
                {
                    if (!(obj is IDictionary<string, object>))
                    {
                        if (!(obj is IProperties))
                        {
                            return false;
                        }
                        IProperties properties = obj as IProperties;
                        if (!properties.Contains(text))
                        {
                            return false;
                        }
                        obj = properties[text];
                    }
                    else
                    {
                        IDictionary<string, object> dictionary = obj as IDictionary<string, object>;
                        if (!dictionary.ContainsKey(text))
                        {
                            return false;
                        }
                        obj = dictionary[text];
                    }
                }
                else
                {
                    ArrayList arrayList = obj as ArrayList;
                    if (!int.TryParse(text, out var result))
                    {
                        return false;
                    }
                    if (result < 0 || result >= arrayList.Count)
                    {
                        return false;
                    }
                    obj = arrayList[result];
                }
            }
            return true;
        }

        private object GetValue(string text2)
        {
            string[] array = text2.Split(new char[3] { '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            object obj = _values;
            string[] array2 = array;
            foreach (string text in array2)
            {
                if (!(obj is ArrayList))
                {
                    if (!(obj is IDictionary<string, object>))
                    {
                        if (obj is IProperties)
                        {
                            obj = (obj as IProperties)[text];
                        }
                    }
                    else
                    {
                        obj = (obj as IDictionary<string, object>)[text];
                    }
                }
                else
                {
                    obj = (obj as ArrayList)[int.Parse(text)];
                }
            }
            return obj;
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PropertyCollection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

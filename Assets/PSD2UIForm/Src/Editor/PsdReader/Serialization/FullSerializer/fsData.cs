using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public sealed class fsData
    {
        private object _value;

        public static readonly fsData True = new fsData(boolean: true);

        public static readonly fsData False = new fsData(boolean: false);

        public static readonly fsData Null = new fsData();

        internal static fsData s_ObfuscationSentinel;

        public fsDataType Type
        {
            get
            {
                if (_value != null)
                {
                    if (!(_value is double))
                    {
                        if (_value is long)
                        {
                            return fsDataType.Int64;
                        }
                        if (!(_value is bool))
                        {
                            if (_value is string)
                            {
                                return fsDataType.String;
                            }
                            if (!(_value is Dictionary<string, fsData>))
                            {
                                if (_value is List<fsData>)
                                {
                                    return fsDataType.Array;
                                }
                                throw new InvalidOperationException("unknown JSON data type");
                            }
                            return fsDataType.Object;
                        }
                        return fsDataType.Boolean;
                    }
                    return fsDataType.Double;
                }
                return fsDataType.Null;
            }
        }

        public bool IsNull => _value == null;

        public bool IsDouble => _value is double;

        public bool IsInt64 => _value is long;

        public bool IsBool => _value is bool;

        public bool IsString => _value is string;

        public bool IsDictionary => _value is Dictionary<string, fsData>;

        public bool IsList => _value is List<fsData>;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public double AsDouble => Cast<double>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public long AsInt64 => Cast<long>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool AsBool => Cast<bool>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string AsString => Cast<string>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Dictionary<string, fsData> AsDictionary => Cast<Dictionary<string, fsData>>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public List<fsData> AsList => Cast<List<fsData>>();

        public fsData()
        {
            _value = null;
        }

        public fsData(bool boolean)
        {
            _value = boolean;
        }

        public fsData(double f)
        {
            _value = f;
        }

        public fsData(long i)
        {
            _value = i;
        }

        public fsData(string str)
        {
            _value = str;
        }

        public fsData(Dictionary<string, fsData> dict)
        {
            _value = dict;
        }

        public fsData(List<fsData> list)
        {
            _value = list;
        }

        public static fsData CreateDictionary()
        {
            return new fsData(new Dictionary<string, fsData>(fsGlobalConfig.IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase));
        }

        public static fsData CreateList()
        {
            return new fsData(new List<fsData>());
        }

        public static fsData CreateList(int capacity)
        {
            return new fsData(new List<fsData>(capacity));
        }

        internal void BecomeDictionary()
        {
            _value = new Dictionary<string, fsData>();
        }

        internal fsData Clone()
        {
            return new fsData
            {
                _value = _value
            };
        }

        private T Cast<T>()
        {
            if (!(_value is T))
            {
                throw new InvalidCastException("Unable to cast <" + this?.ToString() + "> (with type = " + _value.GetType()?.ToString() + ") to type " + typeof(T));
            }
            return (T)_value;
        }

        public override string ToString()
        {
            return fsJsonPrinter.CompressedJson(this);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as fsData);
        }

        public bool Equals(fsData other)
        {
            if (!(other == null) && Type == other.Type)
            {
                switch (Type)
                {
                default:
                    throw new Exception("Unknown data type");
                case fsDataType.Array:
                {
                    List<fsData> asList = AsList;
                    List<fsData> asList2 = other.AsList;
                    if (asList.Count != asList2.Count)
                    {
                        return false;
                    }
                    int num = 0;
                    while (true)
                    {
                        if (num < asList.Count)
                        {
                            if (!asList[num].Equals(asList2[num]))
                            {
                                break;
                            }
                            num++;
                            continue;
                        }
                        return true;
                    }
                    return false;
                }
                case fsDataType.Object:
                {
                    Dictionary<string, fsData> asDictionary = AsDictionary;
                    Dictionary<string, fsData> asDictionary2 = other.AsDictionary;
                    if (asDictionary.Count != asDictionary2.Count)
                    {
                        return false;
                    }
                    foreach (string key in asDictionary.Keys)
                    {
                        if (asDictionary2.ContainsKey(key))
                        {
                            if (!asDictionary[key].Equals(asDictionary2[key]))
                            {
                                return false;
                            }
                            continue;
                        }
                        return false;
                    }
                    return true;
                }
                case fsDataType.Double:
                    if (AsDouble != other.AsDouble)
                    {
                        return Math.Abs(AsDouble - other.AsDouble) < double.Epsilon;
                    }
                    return true;
                case fsDataType.Int64:
                    return AsInt64 == other.AsInt64;
                case fsDataType.Boolean:
                    return AsBool == other.AsBool;
                case fsDataType.String:
                    return AsString == other.AsString;
                case fsDataType.Null:
                    return true;
                }
            }
            return false;
        }

        public static bool operator ==(fsData a, fsData b)
        {
            if ((object)a == b)
            {
                return true;
            }
            if ((object)a != null && (object)b != null)
            {
                if (a.IsDouble && b.IsDouble)
                {
                    return Math.Abs(a.AsDouble - b.AsDouble) < double.Epsilon;
                }
                return a.Equals(b);
            }
            return false;
        }

        public static bool operator !=(fsData a, fsData b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return (object)s_ObfuscationSentinel == null;
        }

        internal static fsData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

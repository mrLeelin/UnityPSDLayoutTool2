using System;
using System.Collections;
using System.Collections.Generic;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdDescriptors
{

internal class PsdPropertyBag : IProperties, IEnumerable<KeyValuePair<string, object>>, IEnumerable
{
	private readonly Dictionary<string, object> _properties;

	public int Count => _properties.Count;

	public object this[string property]
	{
		get
		{
			return GetPropertyByPath(property);
		}
		set
		{
			_properties[property] = value;
		}
	}

	public PsdPropertyBag()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_properties = new Dictionary<string, object>();
	}

	public PsdPropertyBag(int P_0)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_properties = new Dictionary<string, object>(P_0);
	}

	public void Add(string key, object value)
	{
		_properties.Add(key, value);
	}

	public bool Contains(string P_0)
	{
		string[] array = P_0.Split(new char[3] { '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
		object obj = _properties;
		string[] array2 = array;
		int num = 0;
		while (true)
		{
			if (num < array2.Length)
			{
				string text = array2[num];
				if (obj is ArrayList)
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
				else if (obj is IDictionary<string, object>)
				{
					IDictionary<string, object> dictionary = obj as IDictionary<string, object>;
					if (!dictionary.ContainsKey(text))
					{
						return false;
					}
					obj = dictionary[text];
				}
				else
				{
					if (!(obj is IProperties))
					{
						break;
					}
					IProperties properties = obj as IProperties;
					if (!properties.Contains(text))
					{
						return false;
					}
					obj = properties[text];
				}
				num++;
				continue;
			}
			return true;
		}
		return false;
	}

	private object GetPropertyByPath(string P_0)
	{
		string[] array = P_0.Split(new char[3] { '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
		object obj = _properties;
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (obj is ArrayList)
			{
				obj = (obj as ArrayList)[int.Parse(text)];
			}
			else if (!(obj is IDictionary<string, object>))
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
		return obj;
	}

	IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
	{
		return _properties.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return _properties.GetEnumerator();
	}
}
}

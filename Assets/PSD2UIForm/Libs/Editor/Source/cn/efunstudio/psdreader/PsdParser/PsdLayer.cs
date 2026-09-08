using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using PsdLayerUtilities;
using PsdProtectionGuards;
using PsdSections;
using UnityEngine;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace cn.efunstudio.psdreader.PsdParser
{

public class PsdLayer
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass61_0
	{
		public Guid placeID;

		public _003C_003Ec__DisplayClass61_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool _003Cget_LinkedLayer_003Eb__0(ILinkedLayer i)
		{
			if (!(i.ID == placeID))
			{
				return false;
			}
			return i.HasDocument;
		}
	}

	private readonly PsdDocument document;

	private readonly LayerRecords records;

	private int left;

	private int top;

	private int right;

	private int bottom;

	private PsdLayer[] childs;

	private PsdLayer parent;

	private ILinkedLayer linkedLayer;

	private LayerChannelDataReader channels;

	private static PsdLayer[] emptyChilds;

	internal Channel[] Channels => channels.Value;

	internal SectionType SectionType => records.SectionType;

	public string Name => records.Name;

	public bool IsVisible => (records.Flags & LayerFlags.Visible) != LayerFlags.Visible;

	public bool IsGroup
	{
		get
		{
			if (records.SectionType != SectionType.Closed)
			{
				return records.SectionType == SectionType.Opend;
			}
			return true;
		}
	}

	internal bool IsFolderClosed => records.SectionType == SectionType.Closed;

	internal bool IsFolderOpen => records.SectionType == SectionType.Opend;

	internal float Opacity => (float)(int)records.Opacity / 255f;

	public int Left => left;

	public int Top => top;

	public int Right => right;

	public int Bottom => bottom;

	public int Width => right - left;

	public int Height => bottom - top;

	internal int Depth => document.FileHeaderSection.Depth;

	public bool IsClipping => records.Clipping;

	internal BlendMode BlendMode => records.BlendMode;

	internal PsdLayer Parent
	{
		get
		{
			return parent;
		}
		set
		{
			parent = value;
		}
	}

	public PsdLayer[] Childs
	{
		get
		{
			if (childs != null)
			{
				return childs;
			}
			return emptyChilds;
		}
		set
		{
			childs = value;
		}
	}

	internal IProperties Resources => records.Resources;

	public PsdDocument Document => document;

	internal LayerRecords Records => records;

	internal ILinkedLayer LinkedLayer
	{
		get
		{
			_003C_003Ec__DisplayClass61_0 CS_0024_003C_003E8__locals3 = new _003C_003Ec__DisplayClass61_0();
			CS_0024_003C_003E8__locals3.placeID = records.PlacedID;
			if (!(CS_0024_003C_003E8__locals3.placeID == Guid.Empty))
			{
				if (linkedLayer == null)
				{
					linkedLayer = document.LinkedLayers.Where((ILinkedLayer i) => i.ID == CS_0024_003C_003E8__locals3.placeID && i.HasDocument).FirstOrDefault();
				}
				return linkedLayer;
			}
			return null;
		}
	}

	internal bool HasImage
	{
		get
		{
			if (records.SectionType == SectionType.Normal)
			{
				if (Width != 0 && Height != 0)
				{
					return true;
				}
				return false;
			}
			return false;
		}
	}

	internal bool HasMask => records.Mask != null;

	internal PsdLayer(PsdBigEndianReader reader, PsdDocument document)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		this.document = document;
		records = LayerRecordReader.ReadLayerRecord(reader);
		records = LayerExtraDataReader.ReadLayerExtraData(reader, records);
		left = records.Left;
		top = records.Top;
		right = records.Right;
		bottom = records.Bottom;
	}

	public string GetPreviewProtectionFingerprint()
	{
		return this.ComputePreviewProtectionFingerprint();
	}

	public bool TryGetStructuralLayerColor(out Color color)
	{
		color = default(Color);
		if (!TryGetSolidFillLayerColor(this, out color))
		{
			if (this.TryGetTextLayerInfo(out var textLayerInfo))
			{
				color = new Color32(textLayerInfo.Color.R, textLayerInfo.Color.G, textLayerInfo.Color.B, textLayerInfo.Color.A);
				return true;
			}
			return false;
		}
		return true;
	}

	private static bool TryGetSolidFillLayerColor(PsdLayer layer, out Color color)
	{
		color = default(Color);
		if (layer != null && layer.IsSolidFillLayer() && layer.Resources != null)
		{
			if (TryReadColorChannel(layer.Resources, "SoCo.Clr.Rd", out var value) && TryReadColorChannel(layer.Resources, "SoCo.Clr.Grn", out var value2) && TryReadColorChannel(layer.Resources, "SoCo.Clr.Bl", out var value3))
			{
				color = new Color32(value, value2, value3, byte.MaxValue);
				return true;
			}
			return false;
		}
		return false;
	}

	private static bool TryReadColorChannel(IProperties props, string key, out byte value)
	{
		value = 0;
		if (props != null && !string.IsNullOrEmpty(key) && props.Contains(key))
		{
			object obj = props[key];
			if (obj == null)
			{
				return false;
			}
			try
			{
				double val = Convert.ToDouble(obj, CultureInfo.InvariantCulture);
				val = Math.Max(0.0, Math.Min(255.0, val));
				value = (byte)Math.Round(val, MidpointRounding.AwayFromZero);
				return true;
			}
			catch
			{
				return false;
			}
		}
		return false;
	}

	internal void ReadChannels(PsdBigEndianReader reader)
	{
		channels = new LayerChannelDataReader(reader, records.ChannelSize, this);
	}

	internal void ComputeBounds()
	{
		SectionType sectionType = records.SectionType;
		if (sectionType != SectionType.Opend && sectionType != SectionType.Closed)
		{
			return;
		}
		int val = int.MaxValue;
		int val2 = int.MaxValue;
		int val3 = int.MinValue;
		int val4 = int.MinValue;
		bool flag = false;
		foreach (PsdLayer item in this.EnumerateSelfAndDescendants())
		{
			if (item != this && item.HasImage)
			{
				if (!item.Resources.Contains("PlLd.Transformation"))
				{
					val = Math.Min(item.Left, val);
					val2 = Math.Min(item.Top, val2);
					val3 = Math.Max(item.Right, val3);
					val4 = Math.Max(item.Bottom, val4);
				}
				else
				{
					double[] array = (double[])item.Resources["PlLd.Transformation"];
					double[] source = new double[4]
					{
						array[0],
						array[2],
						array[4],
						array[6]
					};
					double[] source2 = new double[4]
					{
						array[1],
						array[3],
						array[5],
						array[7]
					};
					int val5 = (int)Math.Ceiling(source.Min());
					int val6 = (int)Math.Ceiling(source.Max());
					int val7 = (int)Math.Ceiling(source2.Min());
					int val8 = (int)Math.Ceiling(source2.Max());
					val = Math.Min(val5, val);
					val2 = Math.Min(val7, val2);
					val3 = Math.Max(val6, val3);
					val4 = Math.Max(val8, val4);
				}
				flag = true;
			}
		}
		if (flag)
		{
			left = val;
			top = val2;
			right = val3;
			bottom = val4;
		}
	}

	static PsdLayer()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		emptyChilds = new PsdLayer[0];
	}
}
}

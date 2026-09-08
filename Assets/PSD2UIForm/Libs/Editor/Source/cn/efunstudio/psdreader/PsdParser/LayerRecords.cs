using System;
using System.Linq;
using PsdPropertyUtilities;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

internal class LayerRecords
{
	private Channel[] channels;

	private LayerMask layerMask;

	private LayerBlendingRanges blendingRanges;

	private IProperties resources;

	private string name;

	private SectionType sectionType;

	private Guid placedID;

	public int Left { get; set; }

	public int Top { get; set; }

	public int Right { get; set; }

	public int Bottom { get; set; }

	public int Width => Right - Left;

	public int Height => Bottom - Top;

	public int ChannelCount
	{
		get
		{
			if (channels != null)
			{
				return channels.Length;
			}
			return 0;
		}
		set
		{
			if (value <= 56)
			{
				channels = new Channel[value];
				for (int i = 0; i < value; i++)
				{
					channels[i] = new Channel();
				}
				return;
			}
			throw new Exception($"Too many channels : {value}");
		}
	}

	internal Channel[] Channels => channels;

	public BlendMode BlendMode { get; set; }

	public byte Opacity { get; set; }

	public bool Clipping { get; set; }

	public LayerFlags Flags { get; set; }

	public int Filter { get; set; }

	public long ChannelSize => channels.Select((Channel item) => item.Size).Aggregate((long v, long n) => v + n);

	public SectionType SectionType => sectionType;

	public Guid PlacedID => placedID;

	public string Name => name;

	public LayerMask Mask => layerMask;

	public object BlendingRanges => blendingRanges;

	public IProperties Resources => resources;

	public void SetExtraRecords(LayerMask layerMask, LayerBlendingRanges blendingRanges, IProperties resources, string name)
	{
		this.layerMask = layerMask;
		this.blendingRanges = blendingRanges;
		this.resources = resources;
		this.name = name;
		this.resources.TryReadPropertyValue(ref this.name, "luni.Name");
		if (!this.resources.TryReadPropertyValue(ref sectionType, "lsct.SectionType"))
		{
			this.resources.TryReadPropertyValue(ref sectionType, "lsdk.SectionType");
		}
		if (!this.resources.Contains("SoLd.Idnt"))
		{
			if (this.resources.Contains("SoLE.Idnt"))
			{
				placedID = this.resources.GetGuidValue("SoLE.Idnt");
			}
		}
		else
		{
			placedID = this.resources.GetGuidValue("SoLd.Idnt");
		}
		Channel[] array = channels;
		foreach (Channel channel in array)
		{
			switch (channel.Type)
			{
			case ChannelType.Alpha:
				if (this.resources.Contains("iOpa"))
				{
					byte b = this.resources.GetByteValue("iOpa", "Opacity");
					channel.Opacity = (float)(int)b / 255f;
				}
				break;
			case ChannelType.Mask:
				if (this.layerMask != null)
				{
					channel.Width = this.layerMask.Width;
					channel.Height = this.layerMask.Height;
				}
				break;
			}
		}
	}

	public void ValidateSize()
	{
		int num = Right - Left;
		int num2 = Bottom - Top;
		if (num < 0 || num2 < 0)
		{
			throw new NotSupportedException($"Invalidated size ({num}, {num2})");
		}
	}

	public LayerRecords()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}

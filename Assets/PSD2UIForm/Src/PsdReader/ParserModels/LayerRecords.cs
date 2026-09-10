using System;
using System.Linq;
using PsdPropertyExtensionsNamespace;

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

        private static LayerRecords s_ObfuscationSentinel;

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
                if (channels == null)
                {
                    return 0;
                }
                return channels.Length;
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
            this.resources.TryGetProperty(ref this.name, "luni.Name");
            if (!this.resources.TryGetProperty(ref sectionType, "lsct.SectionType"))
            {
                this.resources.TryGetProperty(ref sectionType, "lsdk.SectionType");
            }
            if (this.resources.Contains("SoLd.Idnt"))
            {
                placedID = this.resources.GetGuid("SoLd.Idnt");
            }
            else if (this.resources.Contains("SoLE.Idnt"))
            {
                placedID = this.resources.GetGuid("SoLE.Idnt");
            }
            Channel[] array = channels;
            foreach (Channel channel in array)
            {
                switch (channel.Type)
                {
                case ChannelType.Alpha:
                    if (this.resources.Contains("iOpa"))
                    {
                        byte b = this.resources.GetByte("iOpa", "Opacity");
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

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerRecords GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

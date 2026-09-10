using System;
using System.Globalization;
using System.Linq;
using LayerExtraRecordsReaderNamespace;
using PsdBinaryReaderNamespace;
using LayerChannelsReaderNamespace;
using UnityEngine;
using Object = UnityEngine.Object;
using LayerRecordReaderNamespace;
using PsdLayerTraversalExtensionsNamespace;

namespace cn.efunstudio.psdreader.PsdParser
{
    public class PsdLayer
    {
        private readonly PsdDocument document;

        private readonly LayerRecords records;

        private int left;

        private int top;

        private int right;

        private int bottom;

        private PsdLayer[] childs;

        private PsdLayer parent;

        private ILinkedLayer linkedLayer;

        private LayerChannelsReader channels;

        private static PsdLayer[] emptyChilds = new PsdLayer[0];

        private static PsdLayer s_ObfuscationSentinel;

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
                Guid placeID = records.PlacedID;
                if (!(placeID == Guid.Empty))
                {
                    if (linkedLayer == null)
                    {
                        linkedLayer = document.LinkedLayers.Where((ILinkedLayer i) => i.ID == placeID && i.HasDocument).FirstOrDefault();
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
                    if (Width == 0 || Height == 0)
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }
        }

        internal bool HasMask => records.Mask != null;

        internal PsdLayer(PsdBinaryReader reader, PsdDocument document)
        {
            this.document = document;
            records = LayerRecordReader.ReadLayerRecords(reader);
            records = LayerExtraRecordsReader.ReadExtraRecords(reader, records);
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
            if (TryGetSolidFillLayerColor(this, out color))
            {
                return true;
            }
            if (this.TryGetTextLayerInfo(out var textLayerInfo))
            {
                color = (Color32)(new Color32(textLayerInfo.Color.R, textLayerInfo.Color.G, textLayerInfo.Color.B, textLayerInfo.Color.A));
                return true;
            }
            return false;
        }

        public bool IsPreviewPassthroughGroup()
        {
            if (IsGroup && IsVisible && !HasMask && !IsClipping && Opacity >= 0.999f && BlendMode == BlendMode.PassThrough)
            {
                return !HasLayerEffects(Resources);
            }
            return false;
        }

        private static bool HasLayerEffects(IProperties resources)
        {
            if (resources != null)
            {
                if (!resources.Contains("lrFX"))
                {
                    return resources.Contains("lfx2");
                }
                return true;
            }
            return false;
        }

        private static bool TryGetSolidFillLayerColor(PsdLayer layer, out Color color)
        {
            color = default(Color);
            if (layer != null && layer.IsSolidFillLayer() && layer.Resources != null)
            {
                if (TryReadColorChannel(layer.Resources, "SoCo.Clr.Rd", out var value) && TryReadColorChannel(layer.Resources, "SoCo.Clr.Grn", out var value2) && TryReadColorChannel(layer.Resources, "SoCo.Clr.Bl", out var value3))
                {
                    color = (Color32)(new Color32(value, value2, value3, byte.MaxValue));
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
                if (obj != null)
                {
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
            return false;
        }

        internal void ReadChannels(PsdBinaryReader reader)
        {
            channels = new LayerChannelsReader(reader, records.ChannelSize, this);
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
            foreach (PsdLayer item in this.EnumerateDescendantsAndSelf())
            {
                if (item != this && item.HasImage)
                {
                    if (item.Resources.Contains("PlLd.Transformation"))
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
                    else
                    {
                        val = Math.Min(item.Left, val);
                        val2 = Math.Min(item.Top, val2);
                        val3 = Math.Max(item.Right, val3);
                        val4 = Math.Max(item.Bottom, val4);
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

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdLayer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

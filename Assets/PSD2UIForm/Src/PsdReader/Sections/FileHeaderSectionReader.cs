using System;
using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;

namespace FileHeaderSectionReaderNamespace
{
    internal class FileHeaderSectionReader : PsdSectionReader<FileHeaderSection>
    {
        private static FileHeaderSectionReader s_ObfuscationSentinel;

        public FileHeaderSectionReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, false, (object)null)
        {
        }

        public static FileHeaderSection ReadFileHeader(object psdBinaryReader)
        {
            return new FileHeaderSectionReader((PsdBinaryReader)psdBinaryReader).Value;
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out FileHeaderSection result)
        {
            result = default(FileHeaderSection);
            result.NumberOfChannels = reader.ReadInt16();
            result.Height = reader.ReadInt32();
            result.Width = reader.ReadInt32();
            result.Depth = reader.ReadInt16();
            result.ColorMode = reader.ReadColorMode();
            if (result.Depth != 8 && result.Depth != 16)
            {
                throw new NotSupportedException("暂不支持32-bit PSD文件，请先将PSD文档转换为8-bit或16-bit后再使用。");
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static FileHeaderSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

using System;
using System.IO;
using FileHeaderSectionReaderNamespace;
using FileSystemLinkedDocumentResolverNamespace;
using PsdBinaryReaderNamespace;

namespace cn.efunstudio.psdreader.PsdParser
{
    internal struct FileHeaderSection
    {
        private static object s_ObfuscationSentinel;

        public int Depth { get; set; }

        public int NumberOfChannels { get; set; }

        public ColorMode ColorMode { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public static FileHeaderSection FromFile(string filename)
        {
            using FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PsdBinaryReader value = new PsdBinaryReader(fileStream, new FileSystemLinkedDocumentResolver(), new Uri(Path.GetDirectoryName(filename)));
            value.ReadFileHeaderPreamble();
            return FileHeaderSectionReader.ReadFileHeader(value);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

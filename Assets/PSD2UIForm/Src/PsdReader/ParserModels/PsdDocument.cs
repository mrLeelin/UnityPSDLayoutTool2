using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using FileHeaderSectionReaderNamespace;
using LayerAndMaskSectionReaderNamespace;
using FileSystemLinkedDocumentResolverNamespace;
using PsdBinaryReaderNamespace;
using ImageResourcesSectionReaderNamespace;
using ImageDataSectionReaderNamespace;
using ColorModeDataSectionReaderNamespace;
using LinkedDocumentResolverNamespace;
using PsdPropertyExtensionsNamespace;

namespace cn.efunstudio.psdreader.PsdParser
{
    public class PsdDocument : IDisposable
    {
        private FileHeaderSectionReader fileHeaderSection;

        private ColorModeDataSectionReader colorModeDataSection;

        private ImageResourcesSectionReader imageResourcesSection;

        private LayerAndMaskSectionReader layerAndMaskSection;

        private ImageDataSectionReader imageDataSection;

        private PsdBinaryReader reader;

        [CompilerGenerated]
        private EventHandler m_Disposed;

        internal static PsdDocument s_ObfuscationSentinel;

        internal FileHeaderSection FileHeaderSection => fileHeaderSection.Value;

        internal byte[] ColorModeData => colorModeDataSection.Value;

        public int Width => fileHeaderSection.Value.Width;

        public int Height => fileHeaderSection.Value.Height;

        public int Depth => fileHeaderSection.Value.Depth;

        public PsdLayer[] Childs => layerAndMaskSection.Value.Layers;

        internal IEnumerable<ILinkedLayer> LinkedLayers => layerAndMaskSection.Value.GetLinkedLayers();

        internal IProperties Resources => layerAndMaskSection.Value.Resources;

        internal IProperties ImageResources => imageResourcesSection;

        internal bool HasImage
        {
            get
            {
                if (imageResourcesSection.Contains("Version"))
                {
                    return imageResourcesSection.GetBoolean("Version", "HasCompatibilityImage");
                }
                return false;
            }
        }

        internal event EventHandler Disposed
        {
            [CompilerGenerated]
            add
            {
                EventHandler current = m_Disposed;
                EventHandler observed;
                do
                {
                    observed = current;
                    EventHandler combined = (EventHandler)Delegate.Combine(observed, value);
                    current = Interlocked.CompareExchange(ref m_Disposed, combined, observed);
                }
                while ((object)current != observed);
            }
            [CompilerGenerated]
            remove
            {
                EventHandler current = m_Disposed;
                EventHandler observed;
                do
                {
                    observed = current;
                    EventHandler removed = (EventHandler)Delegate.Remove(observed, value);
                    current = Interlocked.CompareExchange(ref m_Disposed, removed, observed);
                }
                while ((object)current != observed);
            }
        }

        internal PsdDocument()
        {
        }

        public static PsdDocument Create(string filename)
        {
            return Create(filename, new FileSystemLinkedDocumentResolver());
        }

        internal static PsdDocument Create(string filename, LinkedDocumentResolver resolver)
        {
            PsdDocument psdDocument = new PsdDocument();
            FileInfo fileInfo = new FileInfo(filename);
            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                psdDocument.Read(fileStream, resolver, new Uri(fileInfo.DirectoryName));
                return psdDocument;
            }
            catch
            {
                psdDocument.Dispose();
                fileStream?.Dispose();
                throw;
            }
        }

        public static PsdDocument Create(Stream stream)
        {
            return Create(stream, null);
        }

        internal static PsdDocument Create(Stream stream, LinkedDocumentResolver resolver)
        {
            PsdDocument psdDocument = new PsdDocument();
            try
            {
                psdDocument.Read(stream, resolver, new Uri(Directory.GetCurrentDirectory()));
                return psdDocument;
            }
            catch
            {
                psdDocument.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
                OnDisposed(EventArgs.Empty);
            }
        }

        protected virtual void OnDisposed(EventArgs e)
        {
            if (m_Disposed != null)
            {
                m_Disposed(this, e);
            }
        }

        internal void Read(Stream stream, LinkedDocumentResolver resolver, Uri uri)
        {
            reader = new PsdBinaryReader(stream, resolver, uri);
            reader.ReadFileHeaderPreamble();
            fileHeaderSection = new FileHeaderSectionReader(reader);
            colorModeDataSection = new ColorModeDataSectionReader(reader);
            imageResourcesSection = new ImageResourcesSectionReader(reader);
            layerAndMaskSection = new LayerAndMaskSectionReader(reader, this);
            imageDataSection = new ImageDataSectionReader(reader, this);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

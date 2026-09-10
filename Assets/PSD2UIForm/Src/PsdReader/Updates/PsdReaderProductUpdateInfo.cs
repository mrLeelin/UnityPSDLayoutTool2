using System;
using System.Runtime.CompilerServices;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    public sealed class PsdReaderProductUpdateInfo
    {
        [CompilerGenerated]
        private PsdReaderProductUpdateStatus _003CStatus_003Ek__BackingField = PsdReaderProductUpdateStatus.UnknownError;

        [CompilerGenerated]
        private bool _003CHasUpdate_003Ek__BackingField;

        [CompilerGenerated]
        private string _003CCurrentVersion_003Ek__BackingField = string.Empty;

        [CompilerGenerated]
        private string _003CLatestVersion_003Ek__BackingField = string.Empty;

        [CompilerGenerated]
        private string _003CDownloadUrl_003Ek__BackingField = string.Empty;

        [CompilerGenerated]
        private string _003CReleaseNotes_003Ek__BackingField = string.Empty;

        [CompilerGenerated]
        private string _003CPublishedAtUtc_003Ek__BackingField = string.Empty;

        [CompilerGenerated]
        private string _003CMessage_003Ek__BackingField = string.Empty;

        internal static PsdReaderProductUpdateInfo s_ObfuscationSentinel;

        public PsdReaderProductUpdateStatus Status
        {
            [CompilerGenerated]
            get
            {
                return _003CStatus_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CStatus_003Ek__BackingField = value;
            }
        }

        public bool HasUpdate
        {
            [CompilerGenerated]
            get
            {
                return _003CHasUpdate_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CHasUpdate_003Ek__BackingField = value;
            }
        }

        public string CurrentVersion
        {
            [CompilerGenerated]
            get
            {
                return _003CCurrentVersion_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CCurrentVersion_003Ek__BackingField = value;
            }
        }

        public string LatestVersion
        {
            [CompilerGenerated]
            get
            {
                return _003CLatestVersion_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CLatestVersion_003Ek__BackingField = value;
            }
        }

        public string DownloadUrl
        {
            [CompilerGenerated]
            get
            {
                return _003CDownloadUrl_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CDownloadUrl_003Ek__BackingField = value;
            }
        }

        public string ReleaseNotes
        {
            [CompilerGenerated]
            get
            {
                return _003CReleaseNotes_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CReleaseNotes_003Ek__BackingField = value;
            }
        }

        public string PublishedAtUtc
        {
            [CompilerGenerated]
            get
            {
                return _003CPublishedAtUtc_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CPublishedAtUtc_003Ek__BackingField = value;
            }
        }

        public string Message
        {
            [CompilerGenerated]
            get
            {
                return _003CMessage_003Ek__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                _003CMessage_003Ek__BackingField = value;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderProductUpdateInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}

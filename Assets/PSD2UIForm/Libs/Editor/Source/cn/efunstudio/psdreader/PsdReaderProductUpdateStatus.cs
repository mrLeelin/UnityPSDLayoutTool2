namespace cn.efunstudio.psdreader
{

public enum PsdReaderProductUpdateStatus : byte
{
	UpToDate = 1,
	UpdateAvailable,
	NotConfigured,
	UpdateInfoMissing,
	NetworkError,
	InvalidData,
	ProductMismatch,
	UnknownError
}
}

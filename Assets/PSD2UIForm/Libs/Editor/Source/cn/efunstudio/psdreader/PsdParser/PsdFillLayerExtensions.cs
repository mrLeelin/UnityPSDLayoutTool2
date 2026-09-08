namespace cn.efunstudio.psdreader.PsdParser
{

public static class PsdFillLayerExtensions
{
	public static bool IsSolidFillLayer(this PsdLayer layer)
	{
		if (layer != null && layer.Resources != null)
		{
			return layer.Resources.Contains("SoCo.Clr");
		}
		return false;
	}
}
}

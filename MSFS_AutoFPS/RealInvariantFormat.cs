using System;
using System.Globalization;

namespace MSFS_AutoFPS;

public class RealInvariantFormat : IFormatProvider
{
	public NumberFormatInfo formatInfo = CultureInfo.InvariantCulture.NumberFormat;

	public RealInvariantFormat(string value)
	{
		if (value == null)
		{
			formatInfo = new CultureInfo("en-US").NumberFormat;
			return;
		}
		int num = value.LastIndexOf('.');
		if (value.LastIndexOf(',') > num)
		{
			formatInfo = new CultureInfo("de-DE").NumberFormat;
		}
		else
		{
			formatInfo = new CultureInfo("en-US").NumberFormat;
		}
	}

	public object GetFormat(Type formatType)
	{
		if (formatType == typeof(NumberFormatInfo))
		{
			return formatInfo;
		}
		return null;
	}
}

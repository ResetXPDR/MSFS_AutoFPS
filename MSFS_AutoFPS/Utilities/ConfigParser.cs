using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MSFS_AutoFPS.Utilities;

public class ConfigParser
{
	public string GetOrModifyItem(string configFilePath, string itemName, int occurrence, string startingItemName = null, int startingItemOccurrence = 1, string newValue = null)
	{
		string[] array = File.ReadAllLines(configFilePath, Encoding.UTF8);
		Regex regex = new Regex("^(\\s*)(\\w+)(\\s+\"?)([^\"]+)(\"?\\s*)$");
		int num = 0;
		int num2 = 0;
		bool flag = string.IsNullOrEmpty(startingItemName);
		bool flag2 = false;
		for (int i = 0; i < array.Length; i++)
		{
			if (!flag && array[i].Contains(startingItemName, StringComparison.OrdinalIgnoreCase))
			{
				num2++;
				if (num2 == startingItemOccurrence)
				{
					flag = true;
				}
			}
			if (!flag || !regex.IsMatch(array[i]))
			{
				continue;
			}
			Match match = regex.Match(array[i]);
			string value = match.Groups[1].Value;
			string value2 = match.Groups[2].Value;
			string value3 = match.Groups[3].Value;
			string value4 = match.Groups[4].Value;
			string value5 = match.Groups[5].Value;
			if (!value2.Equals(itemName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			num++;
			if (num == occurrence)
			{
				if (newValue == null)
				{
					return value4;
				}
				array[i] = $"{value}{value2}{value3}{newValue}{value5}";
				flag2 = true;
			}
		}
		if (flag2)
		{
			File.WriteAllLines(configFilePath, array, Encoding.UTF8);
		}
		return null;
	}
}

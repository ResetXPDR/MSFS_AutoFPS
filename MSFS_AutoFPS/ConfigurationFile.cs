using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace MSFS_AutoFPS;

public class ConfigurationFile
{
	private Dictionary<string, string> appSettings = new Dictionary<string, string>();

	private XmlDocument xmlDoc = new XmlDocument();

	private string ConfigFile = App.ConfigFile;

	private string ConfigFileLast = App.ConfigFile;

	public string this[string key]
	{
		get
		{
			return GetSetting(key);
		}
		set
		{
			SetSetting(key, value);
		}
	}

	public bool LoadConfiguration(bool isSim2024, bool Common = false)
	{
		ref string configFile = ref ConfigFile;
		string text;
		if (Common)
		{
			text = App.ConfigFileCommon;
		}
		else
		{
			text = (isSim2024 ? App.ConfigFile2024 : App.ConfigFile);
		}
		configFile = text;
		if (!EnsureValidAppSettingsXml(ConfigFile))
		{
			if (!RestoreFromTemplate(ConfigFile, Common, isSim2024))
			{
				return false;
			}
			ConfigFileLast = null;
		}
		try
		{
			xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(File.ReadAllText(ConfigFile));
			XmlElement? documentElement = xmlDoc.DocumentElement;
			appSettings.Clear();
			foreach (XmlNode childNode in documentElement.ChildNodes)
			{
				appSettings.Add(childNode.Attributes["key"].Value, childNode.Attributes["value"].Value);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to parse configuration file after repair. Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			Logger.Log(LogLevel.Error, "ConfigurationFile:LoadConfiguration", "Final parse failure: " + ex.Message);
			Application.Current.Shutdown();
			return false;
		}
		if (!Common && ConfigFile != ConfigFileLast)
		{
			ConfigFileLast = ConfigFile;
			return true;
		}
		return false;
	}

	private static bool EnsureValidAppSettingsXml(string path)
	{
		List<string> list;
		try
		{
			list = File.ReadAllLines(path).ToList();
		}
		catch
		{
			return false;
		}
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		bool flag = false;
		Regex regex = new Regex("^<add\\s+key=\"[^\"]+\"\\s+value=\"[^\"]+\"\\s*/>$", RegexOptions.Compiled);
		foreach (string item in list)
		{
			string text = item.Trim();
			if (text.StartsWith("<?xml"))
			{
				flag = true;
				list2.Add(text);
			}
			else if (text.StartsWith("<add"))
			{
				if (!text.EndsWith("/>"))
				{
					text = (text.EndsWith(">") ? (text.Substring(0, text.Length - 1) + " />") : (text + " />"));
				}
				if (regex.IsMatch(text))
				{
					list3.Add("  " + text);
				}
			}
		}
		if (!flag)
		{
			list2.Insert(0, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
		}
		list2.Add("<appSettings>");
		list2.AddRange(list3);
		list2.Add("</appSettings>");
		if (!list.SequenceEqual(list2))
		{
			Logger.Log(LogLevel.Error, "ConfigurationFile:EnsureValidAppSettingsXml", "Config file repaired due to missing XML header or malformed <add> entries. Saved corrected version to: " + path);
			try
			{
				File.WriteAllLines(path, list2);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "ConfigurationFile:EnsureValidAppSettingsXml", "Repair failed: " + ex.Message);
				return false;
			}
		}
		return true;
	}

	private static bool RestoreFromTemplate(string targetPath, bool Common, bool isSim2024)
	{
		string text;
		if (Common)
		{
			text = "Common";
		}
		else
		{
			text = (isSim2024 ? "MSFS 2024" : "MSFS 2020");
		}
		string text2;
		if (Common)
		{
			text2 = Directory.GetCurrentDirectory() + "\\MSFS_AutoFPS.config";
		}
		else
		{
			text2 = (isSim2024 ? (Directory.GetCurrentDirectory() + "\\MSFS2024_AutoFPS.config") : (Directory.GetCurrentDirectory() + "\\MSFS2020_AutoFPS.config"));
		}
		MessageBox.Show("The " + text + " configuration file was found to be corrupted. It has been recreated from the default template, thus resetting all applicable settings.", "Configuration Reset", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		if (!File.Exists(text2))
		{
			MessageBox.Show("No valid configuration template found for " + text + "! Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			Application.Current.Shutdown();
			return false;
		}
		try
		{
			File.Copy(text2, targetPath, overwrite: true);
			Logger.Log(LogLevel.Information, "ConfigurationFile:RestoreFromTemplate", "Recreated config file from template: '" + text2 + "'");
			return true;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to recreate configuration file from template. Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			Logger.Log(LogLevel.Error, "ConfigurationFile:RestoreFromTemplate", "Template copy failed: " + ex.Message);
			Application.Current.Shutdown();
			return false;
		}
	}

	public void SaveConfiguration()
	{
		foreach (XmlNode item in xmlDoc.ChildNodes[1])
		{
			item.Attributes["value"].Value = appSettings[item.Attributes["key"].Value];
		}
		xmlDoc.Save(ConfigFile);
	}

	public bool SettingExists(string key)
	{
		if (appSettings.ContainsKey(key))
		{
			return true;
		}
		return false;
	}

	public string GetSetting(string key, string defaultValue = "", bool noAdd = false)
	{
		if (appSettings.ContainsKey(key))
		{
			return appSettings[key];
		}
		if (!noAdd)
		{
			AddSetting(key, defaultValue);
		}
		return defaultValue;
	}

	public void AddSetting(string key, string defaultValue = "")
	{
		if (!SettingExists(key))
		{
			XmlNode xmlNode = xmlDoc.CreateElement("add");
			XmlAttribute xmlAttribute = xmlDoc.CreateAttribute("key");
			xmlAttribute.Value = key;
			xmlNode.Attributes.Append(xmlAttribute);
			xmlAttribute = xmlDoc.CreateAttribute("value");
			xmlAttribute.Value = defaultValue;
			xmlNode.Attributes.Append(xmlAttribute);
			xmlDoc.ChildNodes[1].AppendChild(xmlNode);
			appSettings.Add(key, defaultValue);
			SaveConfiguration();
		}
	}

	public void RemoveSetting(string key)
	{
		if (appSettings.ContainsKey(key))
		{
			XmlNode xmlNode = xmlDoc.SelectSingleNode("//add[@key='" + key + "']");
			if (xmlNode != null)
			{
				xmlNode.ParentNode.RemoveChild(xmlNode);
				appSettings.Remove(key);
				SaveConfiguration();
			}
		}
	}

	public void SetSetting(string key, string value)
	{
		if (appSettings.ContainsKey(key))
		{
			appSettings[key] = value;
			SaveConfiguration();
		}
		else
		{
			AddSetting(key, value);
		}
	}

	public void CleanOffsetModuleBaseKeys()
	{
		Dictionary<string, (string, Version)> dictionary = new Dictionary<string, (string, Version)>();
		foreach (KeyValuePair<string, string> appSetting in appSettings)
		{
			if (appSetting.Key.StartsWith("offsetModuleBase ") && Version.TryParse(appSetting.Key.Substring("offsetModuleBase ".Length), out Version result))
			{
				string key = $"{result.Major}.{result.Minor}";
				if (!dictionary.ContainsKey(key) || result > dictionary[key].Item2)
				{
					dictionary[key] = (appSetting.Key, result);
				}
			}
		}
		HashSet<string> hashSet = dictionary.OrderByDescending((KeyValuePair<string, (string fullKey, Version version)> kvp) => kvp.Key).Take(2).Select((KeyValuePair<string, (string fullKey, Version version)> kvp) => kvp.Value.fullKey)
			.ToHashSet();
		foreach (string item in appSettings.Keys.Where((string k) => k.StartsWith("offsetModuleBase ")).ToList())
		{
			if (!hashSet.Contains(item))
			{
				RemoveSetting(item);
			}
		}
	}
}

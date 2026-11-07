namespace AppConfig;

public static class Config
{
	public const string AppVersion = "0.5.5.0";

	public const bool TestVersion = false;

	public const string TestVariant = "-test1";

	public const string AppVersionFull = "0.5.5.0";

	public const string WidgetVersion = "1.6.0";

	public const bool isAutoFPS = true;

	public const string AppNameFull = "MSFS_AutoFPS";

	public const string AppNameShort = "AutoFPS";

	// Fork: all update checks, downloads and help/issue links point at this repository.
	// Upstream (ResetXPDR) releases are never downloaded or executed by this build.
	public const string RepoOwner = "dankocrnkovic";

	public const string RepoName = "MSFS_AutoFPS";

	public const string RepoUrl = "https://github.com/" + RepoOwner + "/" + RepoName;

	public const string ReleasesApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases";
}

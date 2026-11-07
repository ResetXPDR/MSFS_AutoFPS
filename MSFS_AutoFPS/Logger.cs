using System.Collections;
using Serilog;

namespace MSFS_AutoFPS;

public static class Logger
{
	public static readonly Queue MessageQueue = new Queue();

	public static void Log(LogLevel level, string context, string message)
	{
		string messageTemplate = string.Format("[ {0,-32} ] {1}", (context.Length <= 32) ? context : context.Substring(0, 32), message.Replace("\n", "").Replace("\r", "").Replace("\t", ""));
		switch (level)
		{
		case LogLevel.Critical:
			Serilog.Log.Logger.Fatal(messageTemplate);
			break;
		case LogLevel.Error:
			Serilog.Log.Logger.Error(messageTemplate);
			break;
		case LogLevel.Warning:
			Serilog.Log.Logger.Warning(messageTemplate);
			break;
		case LogLevel.Information:
			Serilog.Log.Logger.Information(messageTemplate);
			break;
		case LogLevel.Debug:
			Serilog.Log.Logger.Debug(messageTemplate);
			break;
		case LogLevel.Verbose:
			Serilog.Log.Logger.Verbose(messageTemplate);
			break;
		default:
			Serilog.Log.Logger.Debug(messageTemplate);
			break;
		}
		if (level > LogLevel.Debug)
		{
			if (message.Length > 80)
			{
				MessageQueue.Enqueue(message.Substring(1, 79));
			}
			else
			{
				MessageQueue.Enqueue(message);
			}
		}
	}
}

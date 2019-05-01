using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
	public static class Logger
	{
		// Example of a logging handler. This can be re-used by addons
		// that ask for a Func<LogMessage, Task>.
		public static Task Log(LogMessage message)
		{
			ConsoleColor consoleColor = Console.ForegroundColor;

			switch (message.Severity)
			{
				case LogSeverity.Critical:
				case LogSeverity.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogSeverity.Info:
					Console.ForegroundColor = ConsoleColor.White;
					break;
				case LogSeverity.Verbose:
				case LogSeverity.Debug:
					Console.ForegroundColor = ConsoleColor.DarkGray;
					break;
			}

			Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message ?? message.Exception?.Message} | {message.Exception?.InnerException?.Message}");
			Console.ForegroundColor = consoleColor;

			return Task.CompletedTask;
		}
	}
}

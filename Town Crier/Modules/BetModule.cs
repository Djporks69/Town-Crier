using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using System.Collections.Generic;
using Discord.Rest;
using System.Linq;
using System;
using DiscordBot.Modules.ChatCraft;

using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
	[Group("bet")]
	public class BetModule : CrierModuleBase
	{
		[Command, Alias("help")]
		public async Task Help()
		{
			string prefix = "!bet ";

			List<string> commands = new List<string>();
			List<string> descriptions = new List<string>();

			string message = $"Welcome to the betting system!\n\nHere are the things you'll need to know:\n\n";

			commands.Add("help");
			descriptions.Add("This lovely introduction");
			commands.Add("info");
			descriptions.Add("Description of the current bet");
			commands.Add("list");
			descriptions.Add("View a list of current bets");
			commands.Add("[value]");
			descriptions.Add("Place a bet on a value");

			message += ShowCommands(prefix, commands, descriptions);

			await ReplyAsync(message);
		}

		[Command("info")]
		public async Task Info()
		{
			await ReplyAsync("Current bet: What distance (in km) do you think our furthest travelled player has gone?");
		}

		[Command("list")]
		public async Task List()
		{
			string result = "List of bets:";

			foreach (Player player in ChatCraft.Instance.State.players)
			{
				if (player.bet != 0)
				{
					result += $"\n- {player.name}: {player.bet}";
				}
			}

			await ReplyAsync(result);
		}

		[Command]
		public async Task Bet(int value)
		{
			GetPlayer().bet = value;

			await ReplyAsync("Bet recorded");
		}
	}
}
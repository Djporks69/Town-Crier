using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	static class DoYouCare
	{
		public static async Task Process(SocketUserMessage message)
		{
			if (message.Content.ToLower().Contains("do you care") && message.Content.Contains("?"))
			{
				if (new Random().NextDouble() < 0.05f)
				{
					await message.Channel.SendMessageAsync(message.Author.Mention + " - I am care free.");
				}
				else
				{
					await message.Channel.SendMessageAsync(message.Author.Mention + " - No.");
				}

				return;
			}
		}
	}
}

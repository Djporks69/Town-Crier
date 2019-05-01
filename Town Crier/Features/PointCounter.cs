using Discord.WebSocket;
using DiscordBot.Modules.ChatCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	static class PointCounter
	{
		public static void Process(SocketUserMessage message)
		{
			Player player = ChatCraft.Instance.GetPlayer(message.Author);

			DateTime now = DateTime.UtcNow;

			if ((now - player.lastMessage).TotalMinutes > 1)
			{
				if (player.usedHourPoints > 0)
				{
					if ((now - player.usedFirstHourPoint).TotalHours > 1)
					{
						player.usedHourPoints = 0;
					}
				}

				if (player.usedHourPoints < 20)
				{
					if (player.usedHourPoints == 0)
					{
						player.usedFirstHourPoint = now;
					}

					player.usedHourPoints++;

					player.score += 10;

					player.lastMessage = now;
				}
			}
		}
	}
}

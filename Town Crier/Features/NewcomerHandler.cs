using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Modules.ChatCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	static class NewcomerHandler
	{
		static SocketTextChannel logChannel;
		static string gettingStartedChannel;

		public static void Initialize(DiscordSocketClient client)
		{
			client.UserJoined += UserJoined;
			client.UserLeft += AlertTeam;
		}

		static async Task UserJoined(SocketGuildUser user)
		{
			if (logChannel == null)
			{
				logChannel = user.Guild.GetChannel(334933825383563266) as SocketTextChannel;
			}

			if (gettingStartedChannel == null)
			{
				gettingStartedChannel = (user.Guild.GetChannel(450499963999223829) as ITextChannel).Mention;
			}

			RestUserMessage welcome = await logChannel.SendMessageAsync($"A newcomer has arrived. Welcome {user.Mention}!\nCheck out {gettingStartedChannel} (includes download link!)");

			await welcome.AddReactionAsync(Emojis.Wave);

			Console.WriteLine("User Joined : " + user.Username + " . Member Count : " + logChannel.Guild.MemberCount);

			if ((logChannel.Guild.MemberCount % 1000) == 0)
			{
				await logChannel.SendMessageAsync($"We've now hit {logChannel.Guild.MemberCount} members! Wooooo!");

				await Task.Delay(1000 * 20);

				await logChannel.SendMessageAsync($"Partaayyy!");
			}
		}

		static async Task AlertTeam(IUser user)
		{
			IGuildUser guildUser = user as IGuildUser;

			IGuildChannel channel = await guildUser.Guild.GetChannelAsync(444348503569858560);

			await (channel as ISocketMessageChannel).SendMessageAsync("The user: " + user.Username + " left. They joined: " + guildUser.JoinedAt.ToString());
		}
	}
}

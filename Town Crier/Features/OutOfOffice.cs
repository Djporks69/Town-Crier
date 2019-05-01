using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	static class OutOfOffice
	{
		public static async Task Process(SocketUserMessage message)
		{
			DateTime now = DateTime.Now;

			var channel = message.Channel as ITextChannel;

			if (channel != null &&
				(now.Hour >= 23 || now.Hour < 8) &&
				message.MentionedRoles.Any(item => item.Name == "devs" || item.Name == "admins"))
			{

				IReadOnlyCollection<IGuildChannel> channels = await channel.Guild.GetChannelsAsync(CacheMode.CacheOnly);
				ITextChannel bugs = channels.FirstOrDefault(item => item.Name == "bugs") as ITextChannel;
				ITextChannel feedback = channels.FirstOrDefault(item => item.Name == "feedback") as ITextChannel;
				ITextChannel tipsandhelp = channels.FirstOrDefault(item => item.Name == "tips-and-help") as ITextChannel;
				ITextChannel gettingstarted = channels.FirstOrDefault(item => item.Name == "getting-started") as ITextChannel;

				await message.Channel.SendMessageAsync($"Hi {message.Author.Mention}, unfortunately it's {DateTime.Now.ToShortTimeString()} in Sydney right now.\nIf you're new, check out {gettingstarted.Mention}.\nIf you've got a bug, hit up {bugs.Mention}.\nIf you've got feedback, drop it at {feedback.Mention}.\nOtherwise visit {tipsandhelp.Mention} and hopefully someone else can help!");
			}
		}
	}
}

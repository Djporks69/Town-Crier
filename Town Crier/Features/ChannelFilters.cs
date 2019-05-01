using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	public static class ChannelFilters
	{
		static CommandsProcessor commandsProcessor;

		static ulong botId;

		public static void Apply(CommandsProcessor processor)
		{
			ChannelFilters.commandsProcessor = processor;

			botId = processor.Client.CurrentUser.Id;

			processor.AddChannelHandler(401608834155544587, FilterScreenshots);

			processor.AddChannelHandler(352153406397349900, FilterHeadings);
			processor.AddChannelHandler(405211916987006978, FilterHeadings);
			processor.AddChannelHandler(449033945410174977, FilterHeadings);
		}

		static async Task<bool> FilterScreenshots(SocketUserMessage message)
		{
			if (message.Attachments.Count == 0 && !message.Content.Contains(".jpg") && !message.Content.Contains(".png") && !message.Content.Contains(".gif") && !message.Content.Contains(".jpeg"))
			{
				if (message.Author.Id == botId)
				{
					return true;
				}

				ITextChannel discussion = await (message.Channel as IGuildChannel).Guild.GetChannelAsync(453695249219452928) as ITextChannel;

				await discussion.SendMessageAsync(message.Author.Mention + " said in " + (message.Channel as ITextChannel).Mention + " : " + message.Content);

				await message.DeleteAsync();

				await ReplyAndDelete(message, $"Hi {message.Author.Mention}! I've copied your message to {discussion.Mention}. If you move the discussion there, we can keep this channel filled with screenshots!", 10);

				return false;
			}

			return true;
		}

		static async Task<bool> FilterHeadings(SocketUserMessage message)
		{
			if (!message.Content.StartsWith("```"))
			{
				if (message.Author.Id == botId)
				{
					return true;
				}

				ITextChannel channel = (message.Channel as ITextChannel);
				ITextChannel discussion = await channel.Guild.GetChannelAsync(449033901168656403) as ITextChannel;

				await discussion.SendMessageAsync($@"Hi {message.Author.Mention} - Messages in {channel.Mention} require a heading!

To help, I've created a template for you to copy & paste. Be sure to change the placeholder text!
If you were simply trying to discuss someone elses message, this is the place to do so.
Alternatively react with a :thumbsup: to show your support.

` ```Your Heading```
{message.Content}`");

				await message.DeleteAsync();

				await ReplyAndDelete(message, $"Hi {message.Author.Mention}! Your message needs a heading! Let me help you out in {discussion.Mention}.", 10);

				return false;
			}

			return true;
		}

		static async Task<IUserMessage> ReplyAndDelete(SocketUserMessage original, string response, double seconds)
		{
			SocketCommandContext newContext = new SocketCommandContext(commandsProcessor.Client, original);

			return await commandsProcessor.Interactive.ReplyAndDeleteAsync(newContext, response, timeout: TimeSpan.FromSeconds(seconds));
		}
	}
}

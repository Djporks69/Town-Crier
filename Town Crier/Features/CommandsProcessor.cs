using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Features.Wiki;
using DiscordBot.Modules.ChatCraft;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Features
{
	public class CommandsProcessor
	{
		public InteractiveService Interactive { get; }
		public DiscordSocketClient Client { get; }

		readonly IServiceProvider services;
		readonly IServiceCollection map = new ServiceCollection();
		readonly CommandService commands = new CommandService();
		
		readonly Dictionary<ulong, Func<SocketUserMessage, Task<bool>>> channelHandlers = new Dictionary<ulong, Func<SocketUserMessage, Task<bool>>>();

		public CommandsProcessor(DiscordSocketClient client)
		{
			this.Client = client;
			
			map.AddSingleton(client);
			map.AddSingleton<InteractiveService>();
			
			services = map.BuildServiceProvider();

			Interactive = services.GetService<InteractiveService>();
		}

		public async Task Initialize()
		{
			commands.Log += Logger.Log;

			commands.AddTypeReader<Item>(new ItemTypeReader());
			commands.AddTypeReader<Recipe>(new RecipeTypeReader());
			commands.AddTypeReader<Location>(new LocationTypeReader());
			commands.AddTypeReader<Stat>(new StatTypeReader());
			commands.AddTypeReader<Slot>(new SlotTypeReader());
			commands.AddTypeReader<Unit>(new UnitTypeReader());
			commands.AddTypeReader<ItemSet>(new ItemSetTypeReader());
			commands.AddTypeReader<RecipeSet>(new RecipeSetTypeReader());
			commands.AddTypeReader<EncounterSet>(new EncounterSetTypeReader());
			commands.AddTypeReader<ExploreSet>(new ExploreSetTypeReader());

			await commands.AddModulesAsync(Assembly.GetEntryAssembly());
			
			Client.MessageReceived += HandleCommandAsync;
		}

		public void AddChannelHandler(ulong channelId, Func<SocketUserMessage, Task<bool>> handler)
		{
			channelHandlers.Add(channelId, handler);
		}

		public async Task ExecuteCommand(string command, ICommandContext context)
		{
			await commands.ExecuteAsync(context, command, services);
		}

		async Task HandleCommandAsync(SocketMessage arg)
		{
			// Bail out if it's a System Message.
			var message = arg as SocketUserMessage;

			if (message == null)
			{
				return;
			}

			if (channelHandlers.ContainsKey(message.Channel.Id))
			{
				if (!await channelHandlers[message.Channel.Id].Invoke(message))
				{
					return;
				}
			}

			PointCounter.Process(message);

			await OutOfOffice.Process(message);

			await ServerTeamAlert.Process(message);

			// Create a number to track where the prefix ends and the command begins
			int commandStartIndex = 0;

			bool isMentioned = message.HasMentionPrefix(Client.CurrentUser, ref commandStartIndex);

			if (isMentioned || message.HasCharPrefix('!', ref commandStartIndex))
			{
				await CheckCommand(message, commandStartIndex);
			}
			else
			{
				WikiSearcher.Process(message);

				if (isMentioned)
				{
					await DoYouCare.Process(message);
				}
			}
		}

		async Task CheckCommand(SocketUserMessage message, int commandStartIndex)
		{
			// Create a Command Context.
			var context = new SocketCommandContext(Client, message);

			// Execute the command. (result does not indicate a return value, 
			// rather an object stating if the command executed succesfully).
			var result = await commands.ExecuteAsync(context, commandStartIndex, services);

			// Uncomment the following lines if you want the bot
			// to send a message if it failed (not advised for most situations).
			if (!result.IsSuccess)
			{
				if (result.Error != CommandError.UnknownCommand)
				{
					await message.Channel.SendMessageAsync(result.ErrorReason);
				}
				else
				{
					Console.WriteLine(result.Error);
				}
			}
			else
			{
				Console.WriteLine("Success");
			}
		}
	}
}

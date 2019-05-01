using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot;
using DiscordBot.Modules.ChatCraft;
using Discord.Rest;
using System.Linq;
using System.Collections.Generic;
using BugReporter;
using Discord.Addons.Interactive;

using ActivityRoles;
using DiscordBot.Features.Wiki;
using DiscordBot.Features;

class Program
{
	readonly DiscordSocketClient client;
	

	readonly string token;

	static Program program;

	Dictionary<ulong, IRole> inGameRoles = new Dictionary<ulong, IRole>();

	CommandsProcessor commandsProcessor;

	SocketTextChannel logChannel;
	string gettingStartedChannel;

	// Program entry point
	static void Main(string[] args)
	{
		// Call the Program constructor, followed by the 
		// MainAsync method and wait until it finishes (which should be never).
		program = new Program();
		program.MainAsync().GetAwaiter().GetResult();
	}

	Program()
	{
		ChatCraft.Initialize();

		Console.WriteLine(System.IO.Directory.GetCurrentDirectory());

		foreach (string line in System.IO.File.ReadLines(System.IO.Directory.GetCurrentDirectory() + "/token.txt"))
		{
			token = line;
			break;
		}

		client = new DiscordSocketClient(new DiscordSocketConfig
		{
			// How much logging do you want to see?
			LogLevel = LogSeverity.Info,
			AlwaysDownloadUsers = true

			// If you or another service needs to do anything with messages
			// (eg. checking Reactions, checking the content of edited/deleted messages),
			// you must set the MessageCacheSize. You may adjust the number as needed.
			//MessageCacheSize = 50,

			// If your platform doesn't have native websockets,
			// add Discord.Net.Providers.WS4Net from NuGet,
			// add the `using` at the top, and uncomment this line:
			//WebSocketProvider = WS4NetProvider.Instance
		});
		
		// Subscribe the logging handler to both the client and the CommandService.
		client.Log += Logger.Log;
	}

	public static async Task ExecuteCommand(string command, ICommandContext context)
	{
		await program.commandsProcessor.ExecuteCommand(command, context);
	}

	public static SocketGuild GetGuild(ulong id = 334933825383563266)
	{
		return program.client.GetGuild(id);
	}

	private async Task MainAsync()
	{
		await ApiAccess.EnsureLoggedIn();

		commandsProcessor = new CommandsProcessor(client);
		await commandsProcessor.Initialize();

		ChannelFilters.Apply(commandsProcessor);

		NewcomerHandler.Initialize(client);

		client.Ready += ClientReadyAsync;
		
		client.Disconnected += Disconnected;
		
		await EnableRoleManager();

		await client.LoginAsync(TokenType.Bot, token);
		await client.StartAsync();
		
		await client.SetGameAsync("A Chatty Township Tale");

		AccountModule.EnsureLoaded();

		await Task.Delay(-1);
	}

	async Task EnableRoleManager()
	{
		ActivityRoleManager activityRoleManager = new ActivityRoleManager(client);

		//Add roles here.
		//Game Name is a regex expression.
		//Role Name is just the name of the role
		//Activity Flag (optional) is what types of activities to match
		await activityRoleManager.AddActivityRole(new ActivityDefinition("A Township Tale", "in game"));
		await activityRoleManager.AddActivityRole(new ActivityDefinition("A Township Tale", "streaming", ActivityFlag.Streaming));
		//await activityRoleManager.AddActivityRole(new ActivityDefinition("^Final Fantasy", "final fantasy", ActivityFlag.Streaming | ActivityFlag.Playing));
		//await activityRoleManager.AddActivityRole(new ActivityDefinition("^Spotify$", "listening", ActivityFlag.Listening));

		await activityRoleManager.SetEnabled(true);
	}

	async Task Disconnected(Exception ey)
	{
		Console.WriteLine("Disconnected");
	}

	async Task ClientReadyAsync()
	{
		Console.WriteLine("Client Ready");
	}
}
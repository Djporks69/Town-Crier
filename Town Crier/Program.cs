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
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json;
using BugReporter;
using Discord.Addons.Interactive;
using System.Net.Http;
using System.Text.RegularExpressions;

using ActivityRoles;

class Program
{
	const string WikiStart = "{";
	const string WikiEnd = "}";


	readonly DiscordSocketClient client;

	// Keep the CommandService and IServiceCollection around for use with commands.
	readonly IServiceCollection map = new ServiceCollection();
	readonly CommandService commands = new CommandService();

	InteractiveService interactive;

	readonly string token;

	static Program program;

	Dictionary<ulong, IRole> inGameRoles = new Dictionary<ulong, IRole>();

	Dictionary<ulong, Func<SocketUserMessage, Task<bool>>> channelHandles = new Dictionary<ulong, Func<SocketUserMessage, Task<bool>>>();

	Emoji waveEmoji = new Emoji("👋");

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
		channelHandles.Add(401608834155544587, FilterScreenshots);

		channelHandles.Add(352153406397349900, FilterHeadings);
		channelHandles.Add(405211916987006978, FilterHeadings);
		channelHandles.Add(449033945410174977, FilterHeadings);

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
		client.Log += Logger;
		commands.Log += Logger;

		//new ReliabilityService(client, Logger);
	}

	public static async Task ExecuteCommand(string command, ICommandContext context)
	{
		await program.commands.ExecuteAsync(context, command, program._services);
	}

	public static SocketGuild GetGuild(ulong id = 334933825383563266)
	{
		return program.client.GetGuild(id);
	}

	async Task<IUserMessage> ReplyAndDelete(SocketUserMessage original, string response, double seconds)
	{
		return await interactive.ReplyAndDeleteAsync(new SocketCommandContext(client, original), response, timeout: TimeSpan.FromSeconds(seconds));
	}

	async Task<bool> FilterScreenshots(SocketUserMessage message)
	{
		if (message.Attachments.Count == 0 && !message.Content.Contains(".jpg") && !message.Content.Contains(".png") && !message.Content.Contains(".gif") && !message.Content.Contains(".jpeg"))
		{
			if (message.Author.Id == client.CurrentUser.Id)
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

	async Task<bool> FilterHeadings(SocketUserMessage message)
	{
		if (!message.Content.StartsWith("```"))
		{
			if (message.Author.Id == client.CurrentUser.Id)
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

	SocketTextChannel logChannel;
	string gettingStartedChannel;

	private async Task UserJoined(SocketGuildUser user)
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

		await welcome.AddReactionAsync(waveEmoji);

		Console.WriteLine("User Joined : " + user.Username + " . Member Count : " + logChannel.Guild.MemberCount);

		if ((logChannel.Guild.MemberCount % 1000) == 0)
		{
			await logChannel.SendMessageAsync($"We've now hit {logChannel.Guild.MemberCount} members! Wooooo!");

			await Task.Delay(1000 * 20);

			await logChannel.SendMessageAsync($"Partaayyy!");
		}
	}

	//Kudos to Sol's mate
	private async Task OnReactAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
	{
		if (!reaction.User.IsSpecified || !JiraReporter.Settings.CheckAllowed(reaction.User.Value as SocketUser))
		{
			return;
		}

		string type;

		if (reaction.Emote.Name == "🐛")
		{
			type = "Bug";
		}
		else if (reaction.Emote.Name == "🚀")
		{
			type = "Story";
		}
		else
		{
			return;
		}

		SocketTextChannel textChannel = channel as SocketTextChannel;

		RestUserMessage userMessage = channel.GetMessageAsync(message.Id).Result as RestUserMessage;

		Task.Run(() => new JiraReporter().Report(reaction.User.Value, client, userMessage, new InteractiveService(client, TimeSpan.FromMinutes(5)), type));
	}

	// Example of a logging handler. This can be re-used by addons
	// that ask for a Func<LogMessage, Task>.
	private static Task Logger(LogMessage message)
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

	private async Task MainAsync()
	{
		await ApiAccess.EnsureLoggedIn();

		// Centralize the logic for commands into a seperate method.
		await InitCommands();

		client.Ready += ClientReadyAsync;
		
		client.UserJoined += UserJoined;
		
		client.Disconnected += Disconnected;

		client.UserLeft += AlertTeam;

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

	async Task AlertTeam(IUser user)
	{
		IGuildUser guildUser = user as IGuildUser;

		IGuildChannel channel = await guildUser.Guild.GetChannelAsync(444348503569858560);

		await (channel as ISocketMessageChannel).SendMessageAsync("The user: " + user.Username + " left. They joined: " + guildUser.JoinedAt.ToString());
	}

	async Task Disconnected(Exception ey)
	{
		Console.WriteLine("Disconnected");
	}

	async Task ClientReadyAsync()
	{
		Console.WriteLine("Client Ready");
	}

	private IServiceProvider _services;

	private async Task InitCommands()
	{
		// Repeat this for all the service classes
		// and other dependencies that your commands might need.
		//_map.AddSingleton(new SomeServiceClass());

		// When all your required services are in the collection, build the container.
		// Tip: There's an overload taking in a 'validateScopes' bool to make sure
		// you haven't made any mistakes in your dependency graph.

		map.AddSingleton(client);
		map.AddSingleton<InteractiveService>();


		_services = map.BuildServiceProvider();

		interactive = _services.GetService<InteractiveService>();

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

		// Either search the program and add all Module classes that can be found.
		// Module classes *must* be marked 'public' or they will be ignored.
		await commands.AddModulesAsync(Assembly.GetEntryAssembly());

		// Or add Modules manually if you prefer to be a little more explicit:
		//await _commands.AddModuleAsync<SomeModule>();

		// Subscribe a handler to see if a message invokes a command.
		client.MessageReceived += HandleCommandAsync;
		//client.ReactionAdded += OnReactAddedAsync;

	}

	void RewardPoints(SocketUserMessage message)
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

	private async Task HandleCommandAsync(SocketMessage arg)
	{
		// Bail out if it's a System Message.
		var msg = arg as SocketUserMessage;

		if (msg == null)
		{
			return;
		}

		if (channelHandles.ContainsKey(msg.Channel.Id))
		{
			if (!await channelHandles[msg.Channel.Id].Invoke(msg))
			{
				return;
			}
		}

		RewardPoints(msg);

		DateTime now = DateTime.Now;

		var channel = msg.Channel as ITextChannel;

		if (channel != null &&
			(now.Hour >= 23 || now.Hour < 8) &&
			msg.MentionedRoles.Any(item => item.Name == "devs" || item.Name == "admins"))
		{

			IReadOnlyCollection<IGuildChannel> channels = await channel.Guild.GetChannelsAsync(CacheMode.CacheOnly);
			ITextChannel bugs = channels.FirstOrDefault(item => item.Name == "bugs") as ITextChannel;
			ITextChannel feedback = channels.FirstOrDefault(item => item.Name == "feedback") as ITextChannel;
			ITextChannel tipsandhelp = channels.FirstOrDefault(item => item.Name == "tips-and-help") as ITextChannel;
			ITextChannel gettingstarted = channels.FirstOrDefault(item => item.Name == "getting-started") as ITextChannel;

			await msg.Channel.SendMessageAsync($"Hi {msg.Author.Mention}, unfortunately it's {DateTime.Now.ToShortTimeString()} in Sydney right now.\nIf you're new, check out {gettingstarted.Mention}.\nIf you've got a bug, hit up {bugs.Mention}.\nIf you've got feedback, drop it at {feedback.Mention}.\nOtherwise visit {tipsandhelp.Mention} and hopefully someone else can help!");
		}

		// Create a number to track where the prefix ends and the command begins
		int pos = 0;

		if (channel != null && channel.Id != 560633017517867019)
		{			
			if (msg.MentionedRoles.Count > 0 && msg.MentionedRoles.Contains(channel.Guild.GetRole(560631812876009472)))
			{
				ITextChannel text = await channel.Guild.GetTextChannelAsync(560633017517867019);

				IUserMessage message = await text.SendMessageAsync(msg.Author.Mention + " in " + channel.Mention + ": " + msg.Content);

				await message.AddReactionAsync(Emojis.Tick);
			}
		}

		bool isMentioned = msg.HasMentionPrefix(client.CurrentUser, ref pos);

		if (isMentioned && msg.Content.ToLower().Contains("do you care") && msg.Content.Contains("?"))
		{
			if (new Random().NextDouble() < 0.05f)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + " - I am care free.");
			}
			else
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + " - No.");
			}

			return;
		}

		// Replace the '!' with whatever character
		// you want to prefix your commands with.
		// Uncomment the second half if you also want
		// commands to be invoked by mentioning the bot instead.
		if (isMentioned || msg.HasCharPrefix('!', ref pos))
		{
			// Create a Command Context.
			var context = new SocketCommandContext(client, msg);

			// Execute the command. (result does not indicate a return value, 
			// rather an object stating if the command executed succesfully).
			var result = await commands.ExecuteAsync(context, pos, _services);

			// Uncomment the following lines if you want the bot
			// to send a message if it failed (not advised for most situations).
			if (!result.IsSuccess)
			{
				if (result.Error != CommandError.UnknownCommand)
				{
					await msg.Channel.SendMessageAsync(result.ErrorReason);
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
		else if (msg.Content.Contains(WikiStart) && msg.Content.Contains(WikiEnd))
		{
			Task.Run(() => ShowWiki(msg));
		}
	}

	async void ShowWiki(SocketUserMessage message)
	{
		try
		{
			List<string> items = GetWikiItems(message);

			if (items.Count == 0)
			{
				return;
			}

			var builder = new EmbedBuilder()
			.WithColor(new Color(0xC9881E))
			.WithTimestamp(DateTime.UtcNow)
			.WithThumbnailUrl("https://d1u5p3l4wpay3k.cloudfront.net/atownshiptale_gamepedia_en/9/9e/WikiOnly.png")
			.WithAuthor(author =>
			{
				author
				.WithName("A Township Tale Wiki")
				.WithUrl("https://townshiptale.gamepedia.com");
			});

			if (items.Count > 1)
			{
				//Stop items from looking for image
				builder.ImageUrl = builder.ThumbnailUrl;
			}

			foreach (string item in items)
			{
				await GetWikiDescription(item, builder);
			};

			if (items.Count > 1)
			{
				builder.ImageUrl = null;
			}

			Embed embed = builder.Build();

			await message.Channel.SendMessageAsync(
				"Hopefully this will help!",
				embed: embed)
				.ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
		}
	}

	List<string> GetWikiItems(SocketUserMessage message)
	{
		HashSet<string> lowerCase = new HashSet<string>();
		List<string> result = new List<string>();

		int index = 0;

		string modifiedContent = message.Content;

		do
		{
			index = message.Content.IndexOf(WikiStart, index);

			if (index != -1)
			{
				int endIndex = message.Content.IndexOf(WikiEnd, index);

				if (endIndex != -1)
				{
					index += WikiStart.Length;

					int length = endIndex - index;

					if (length == 0 && length > 30)
					{
						continue;
					}

					string content =  message.Content.Substring(index, length);

					if (content.Any(character => !char.IsLetterOrDigit(character) && character != '_' && character != ' ' && character != ':' && character != '#' && character != '.'))
					{
						continue;
					}

					modifiedContent.Replace(WikiStart + content + WikiEnd, content);

					if (lowerCase.Add(content.ToLower()))
					{
						result.Add(content);
					}
				}

				index = endIndex;
			}
		}
		while (index != -1);

		return result;
	}

	async Task GetWikiDescription(string item, EmbedBuilder builder, bool isFixing = true)
	{
		string[] split = item.Split('#');

		item = split[0];
		
		using (HttpClient httpClient = new HttpClient())
		{
			string description = null;

			if (isFixing)
			{
				HttpResponseMessage apiSearch = await httpClient.GetAsync("https://townshiptale.gamepedia.com/api.php?action=opensearch&profile=fuzzy&redirects=resolve&search=" + item);

				//Format of result is really weird (array of mismatched types).
				//Adding in square brackets to make first item a string array (rather than just string)
				string result = apiSearch.Content.ReadAsStringAsync().Result;
				result = result.Insert(1, "[");
				result = result.Insert(result.IndexOf(','), "]");

				string[][] array = JsonConvert.DeserializeObject<string[][]>(result);

				if (array == null || array[1].Length == 0)
				{
					description = "Page not found";

					string url = "https://townshiptale.gamepedia.com/" + item.Replace(" ", "_");

					description += $"\n[Click here to create it!]({url})";
				}
				else
				{
					item = array[1][0];
				}
			}

			if (description == null)
			{
				HttpResponseMessage apiResponseText = await httpClient.GetAsync("https://townshiptale.gamepedia.com/api.php?format=json&action=parse&page=" + item);

				ApiResponse apiResponse = null;

				try
				{
					string text = apiResponseText.Content.ReadAsStringAsync().Result;

					apiResponse = JsonConvert.DeserializeObject<ApiResponse>(text);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
					apiResponse = new ApiResponse();
				}

				if (apiResponse.parse == null)
				{
					if (!isFixing)
					{
						await GetWikiDescription(item, builder, true);
						return;
					}
				}
				else
				{
					string url = "https://townshiptale.gamepedia.com/" + item.Replace(" ", "_");

					int startSearch = 0;

					if (split.Length > 1)
					{
						startSearch = apiResponse.parse.text.value.IndexOf("mw-headline\" id=\"" + split[1], StringComparison.InvariantCultureIgnoreCase);

						if (startSearch < 0)
						{
							startSearch = apiResponse.parse.text.value.IndexOf("<p><b>" + split[1], StringComparison.InvariantCultureIgnoreCase);

							if (startSearch < 0)
							{
								startSearch = 0;
							}
							else
							{
								//Need to look for best subheading

								int headlineStart = apiResponse.parse.text.value.LastIndexOf("mw-headline\" id=\"", startSearch, startSearch) + 17;

								int headlineEnd = apiResponse.parse.text.value.IndexOf("\"", headlineStart);

								string headline = apiResponse.parse.text.value.Substring(headlineStart, headlineEnd - headlineStart);

								split[1] = headline;
							}
						}

						url += '#' + split[1];
					}

					int start = apiResponse.parse.text.value.IndexOf("<p>", startSearch);

					if (start < 0 && startSearch > 0)
					{
						start = apiResponse.parse.text.value.IndexOf("<p>");
					}
					
					if (builder.ImageUrl == null && apiResponse.parse.images.Length > 0)
					{
						for (int i = 0; i < apiResponse.parse.images.Length; i++)
						{
							if (apiResponse.parse.text.value.IndexOf(apiResponse.parse.images[i]) < startSearch)
							{
								continue;
							}

							HttpResponseMessage imageResponse = await httpClient.GetAsync("https://townshiptale.gamepedia.com/api.php?action=query&prop=imageinfo&format=json&iiprop=url&titles=File:" + apiResponse.parse.images[i]);

							string text = imageResponse.Content.ReadAsStringAsync().Result;

							try
							{
								builder.ImageUrl = JsonConvert.DeserializeObject<ImageResponse>(text).Url;
							}
							catch
							{
								builder.ImageUrl = null;
							}

							break;
						}
					}

					if (start >= 0)
					{
						int end = apiResponse.parse.text.value.IndexOf("</p>", start);

						if (end > 0)
						{
							start += 3;

							description = apiResponse.parse.text.value.Substring(start, end - start)
							.Replace("<b>", "**")
							.Replace("</b>", "**")
							.Trim();

							description = Regex.Replace(description, item + "s?", match => $"[{match.Value}]({url})");

							RemoveHtml(ref description);
						}
					}

					if (description == null)
					{
						description = "No description found";
					}

					description += $"\n[Click here for more info]({url})";
				}
			}

			builder.AddField(item, description);
		};
	}

	void RemoveHtml(ref string description)
	{
		int index = 0;

		int linkFrom = -1;
		string link = null;
		
		do
		{
			index = description.IndexOf('<');

			if (index != -1)
			{
				if (linkFrom >= 0)
				{
					if (index - linkFrom > 1)
					{
						string subString = description.Substring(linkFrom, index - linkFrom);

						description = description.Remove(linkFrom, index - linkFrom);

						description = description.Insert(linkFrom, $"[{subString}]({link})");
					}

					linkFrom = -1;
					link = null;

					continue;
				}

				int endIndex = description.IndexOf('>', index);

				if (endIndex != -1)
				{
					int length = endIndex - index + 1;

					if (description[index + 1] == 'a')
					{
						int linkIndex = description.IndexOf("href", index, length);

						if (linkIndex > 0)
						{
							linkIndex += 6;

							int linkEnd = description.IndexOf("\"", linkIndex);

							if (linkEnd > 0)
							{
								linkFrom = index;

								link = description.Substring(linkIndex, linkEnd - linkIndex);
								
								if (link.StartsWith("/"))
								{
									link = "https://townshiptale.gamepedia.com" + link;
								}
							}
						}
					}

					description = description.Remove(index, length);
				}
			}
		}
		while (index != -1);

		description = description.Replace("\n\n", "\n");
	}

	class ApiResponse
	{
		public Parse parse;

		public class Parse
		{
			public class Text
			{
				[JsonProperty("*")]
				public string value;
			}

			public string title;
			public int pageId;
			public int revId;
			public Text text;
			public string displayTitle;
			public string[] images;
		}
	}

	class ImageResponse
	{
		public string Url { get { return query?.pages.FirstOrDefault().Value?.imageInfo[0].url; } }

		public Query query;

		public class Query
		{
			public Dictionary<int, Page> pages;

			public class Page
			{
				public ImagegInfo[] imageInfo;

				public class ImagegInfo
				{
					public string url;
				}
			}
		}
	}
}
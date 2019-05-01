using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Rest;

namespace DiscordBot.Modules.ChatCraft
{
	public static class Emojis
	{
		public static Emoji Wave = new Emoji("👋");
		public static Emoji Repeat { get; } = new Emoji("🔁");
		public static Emoji Tick { get; } = new Emoji("✅");
		public static Emoji Gem { get; } = new Emoji("💎");
	}

	public class EmojiResponse : IReactionCallback
	{
		public SocketCommandContext Context { get; }

		public ICriterion<SocketReaction> Criterion { get; }

		public RunMode RunMode { get; }

		public TimeSpan? Timeout { get; }

		Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>> callbacks;
		
		public EmojiResponse(SocketCommandContext context, ICriterion<SocketReaction> criterion, Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>> callbacks, TimeSpan timeSpan, RunMode runMode = RunMode.Default)
		{
			Context = context;
			Criterion = criterion;
			Timeout = timeSpan;
			RunMode = runMode;

			this.callbacks = callbacks;
		}

		async Task<bool> IReactionCallback.HandleCallbackAsync(SocketReaction reaction)
		{
			Func<SocketReaction, SocketCommandContext, Task<bool>> callback;

			if (callbacks.TryGetValue(reaction.Emote, out callback))
			{
				if (await callback(reaction, Context))
				{
					callbacks.Remove(reaction.Emote);

					if (callbacks.Count == 0)
					{
						return true;
					}
				}
			}

			return false;
		}
	}

	public class CustomContext : SocketCommandContext
	{
		new public IMessageChannel Channel { get { return base.Channel; } set { typeof(SocketCommandContext).GetField("<Channel>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, value); } }
		new public IDiscordClient Client { get { return base.Client; } set { typeof(SocketCommandContext).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, value); } }
		new public IGuild Guild { get { return base.Guild; } set { typeof(SocketCommandContext).GetField("<Guild>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, value); } }
		new public IMessage Message { get { return base.Message; } set { typeof(SocketCommandContext).GetField("<Message>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, value); } }
		new public IUser User { get { return base.User; } set { typeof(SocketCommandContext).GetField("<User>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, value); } }
			
		public CustomContext(SocketCommandContext existing) : base(existing.Client, existing.Message)
		{

		}
	}

	[RequireAdmin]
	[Group("Debug")]
	[Alias("D")]
	public class ChatCraftConfig : CrierModuleBase
	{
		public abstract class ConfigModule<T> : CrierModuleBase
		{
			public delegate bool TryParse<TType>(string input, out TType result);
			
			[Command("Add", RunMode = RunMode.Async)]
			public abstract Task Add();

			[Command("View")]
			public abstract Task View(T target);

			[Command("Edit", RunMode = RunMode.Async)]
			public abstract Task Edit(T target);

			[Command("Remove", RunMode = RunMode.Async)]
			public abstract Task Remove(T target);
			
			public async Task<TType> AsyncAskTryParse<TType>(string question, TryParse<TType> tryParse)
			{
				TType result;

				while (true)
				{
					string response = await AsyncAsk(question);
					
					if (tryParse(response, out result))
					{
						return result;
					}
				}
			}


			public async Task<string> AsyncAsk(string question)
			{
				await ReplyMentionAsync(question);

				SocketMessage message = await Interactive.NextMessageAsync(Context, true, true, TimeSpan.FromMinutes(1));

				return message.Content;
			}

			public async Task AsyncAskListBasic(string question, int minimum, Action<string> handleResponse)
			{
				await AsyncAskList(question, minimum, async result =>
				{
					handleResponse(result);

					return true;
				});
			}

			public async Task AsyncAskList(string question, int minimum, Func<string, Task<bool>> handleResponse)
			{
				List<string> responses = new List<string>();

				for (int i = 0; ; i++)
				{
					if (i == minimum)
					{
						question += " ('x' to skip)";
					}

					string input = await AsyncAsk(question);

					if (i >= minimum && input.Trim().ToLower() == "x")
					{
						break;
					}

					if (!await handleResponse(input))
					{
						break;
					}
				}
			}

			public async Task AsyncRemove<TKey, TValue>(string question, List<TValue> list, Func<string, TKey> find, Func<TKey, TValue, bool> isMatch, Action<TValue> removed)
				where TKey : class
				where TValue : class
			{
				await AsyncAskList("Name an element to remove.", 0, async name =>
				{
					TKey key = find(name);

					if (key == null)
					{
						await ReplyMentionAsync("Item not found!");
					}
					else
					{
						TValue removing = list.FirstOrDefault(test => isMatch(key, test));

						if (removing != null)
						{
							await ReplyMentionAsync("Removed!");
							
							removed(removing);
						}
						else
						{
							await ReplyMentionAsync("Entry not found!");
						}
					}

					return true;
				});
			}
		}


		[Group("ItemSet")]
		public class ItemSetModule : ConfigModule<ItemSet>
		{
			async Task AsyncAdd(ItemSet set)
			{
				await AsyncAskList("Add an item.", 0, async name =>
				{
					Item item = Item.Find(name);

					if (item == null)
					{
						await ReplyMentionAsync($"An item of the name '{name}' could not be found.");
					}
					else
					{
						ItemWeightCount itemWeightCount = new ItemWeightCount()
						{
							item = item,
							minimum = await AsyncAskTryParse<int>($"Minimum", int.TryParse),
							maximum = await AsyncAskTryParse<int>($"Maximum", int.TryParse),
							weight = await AsyncAskTryParse<float>($"Weight", float.TryParse)
						};

						set.itemWeights.Add(itemWeightCount);
					}

					return true;
				});
			}

			public override async Task Add()
			{
				ItemSet set = new ItemSet();

				set.name = await AsyncAsk("What is the name of the item set?");

				await AsyncAdd(set);


				await AsyncAskList("Add a sub set.", 0, async name =>
				{
					ItemSet subSet = ItemSet.Find(name);

					if (subSet == null)
					{
						await ReplyMentionAsync($"An item set of the name '{name}' could not be found.");
					}
					else
					{
						set.subSets.Add(subSet);
					}

					return true;
				});

				ChatCraft.Instance.State.itemSets.Add(set);

				ChatCraft.Instance.Save();

				await ReplyMentionAsync($"Finished adding {set.name}.");
			}

			public override async Task View(ItemSet target)
			{
				string response = target.name + '\n';

				foreach (ItemWeightCount item in target.itemWeights)
				{
					response += $"\n{item.item.name} ({item.minimum} - {item.maximum}) {item.weight}";
				}

				response += '\n';

				foreach (ItemSet itemSet in target.subSets)
				{
					response += $"\n{itemSet.name}";
				}

				await ReplyMentionAsync(response);
			}

			public override async Task Edit(ItemSet target)
			{
				await AsyncAskList("What should the name change to?", 0, name =>
				{
					target.name = name;
					return Task.FromResult(false);
				});

				string message = "These are the current items:";

				foreach (ItemWeightCount item in target.itemWeights)
				{
					message += $"\n{item.item.name} ({item.minimum} - {item.maximum}) {item.weight}";
				}

				await ReplyMentionAsync(message);

				await AsyncRemove("Name an item to remove.", target.itemWeights, Item.Find, (key, weight) => weight.item == key, removing => target.itemWeights.Remove(removing));

				await AsyncAdd(target);

				message = "These are the subsets:";

				foreach (ItemSet itemSet in target.subSets)
				{
					message += $"\n{itemSet.name}";
				}

				await ReplyMentionAsync(message);

				await AsyncRemove("Name a subset to remove.", target.subSets, ItemSet.Find, (key, item) => item == key, removing => target.subSets.Remove(removing));

				await ReplyMentionAsync($"Finished editing {target.name}.");
			}

			public override async Task Remove(ItemSet target)
			{
				if (await AsyncAskTryParse<bool>("You want to delete? (true / false)", bool.TryParse))
				{
					ChatCraft.Instance.State.itemSets.Remove(target);

					foreach (ItemSet itemSet in ChatCraft.Instance.State.itemSets)
					{
						itemSet.subSets.Remove(target);
					}

					foreach (ExploreSet exploreSet in ChatCraft.Instance.State.exploreSets)
					{
						if (exploreSet.itemSet == target)
						{
							exploreSet.itemSet = null;
						}
					}
				}
			}
		}

		[Group("ExploreSet")]
		public class ExploreSetModule : ConfigModule<ExploreSet>
		{
			public override async Task Add()
			{
				ExploreSet exploreSet = new ExploreSet();

				exploreSet.name = await AsyncAsk("What is the name of the explore set?");


				await AsyncAskList("Name the ItemSet.", 0, async name =>
				{
					ItemSet set = ItemSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An ItemSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						exploreSet.itemSet = set;

						return false;
					}
				});

				await AsyncAskList("Name the RecipeSet.", 0, async name =>
				{
					RecipeSet set = RecipeSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An RecipeSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						exploreSet.recipeSet = set;

						return false;
					}
				});

				await AsyncAskList("Name the EncounterSet.", 0, async name =>
				{
					EncounterSet set = EncounterSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An EncounterSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						exploreSet.encounterSet = set;

						return false;
					}
				});


				ChatCraft.Instance.State.exploreSets.Add(exploreSet);

				ChatCraft.Instance.Save();

				await ReplyMentionAsync($"Finished adding {exploreSet.name}.");
			}

			public override async Task View(ExploreSet target)
			{
				string response = target.name + '\n';

				response += $"{target.itemSet?.name}\n";
				response += $"{target.recipeSet?.name}\n";
				response += $"{target.encounterSet?.name}\n";

				await ReplyMentionAsync(response);
			}

			public override async Task Edit(ExploreSet target)
			{
				await AsyncAskList("What should the name change to?", 0, async name =>
				{
					target.name = name;
					return false;
				});

				await AsyncAskList("Name the new ItemSet.", 0, async name =>
				{
					if (name == ".")
					{
						target.itemSet = null;
					}

					ItemSet set = ItemSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An ItemSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						target.itemSet = set;

						return false;
					}
				});

				await AsyncAskList("Name the new RecipeSet.", 0, async name =>
				{
					if (name == ".")
					{
						target.recipeSet = null;
					}

					RecipeSet set = RecipeSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An RecipeSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						target.recipeSet = set;

						return false;
					}
				});

				await AsyncAskList("Name the new EncounterSet.", 0, async name =>
				{
					if (name == ".")
					{
						target.encounterSet = null;
					}

					EncounterSet set = EncounterSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An EncounterSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						target.encounterSet = set;

						return false;
					}
				});

				await ReplyMentionAsync($"Finished editing {target.name}.");
			}

			public override async Task Remove(ExploreSet target)
			{
				if (await AsyncAskTryParse<bool>("You want to delete? (true / false)", bool.TryParse))
				{
					ChatCraft.Instance.State.exploreSets.Remove(target);
				}
			}
		}

		[Group("Location")]
		public class LocationModule : ConfigModule<Location>
		{
			async Task AsyncAddConnections(Location location)
			{
				await AsyncAskList("Name the connections.", 1, async name =>
				{
					Location other = Location.Find(name);

					if (other == null)
					{
						await ReplyMentionAsync($"A location of the name '{name}' could not be found.");
					}
					else
					{
						Connection connection = new Connection()
						{
							locationA = location,
							locationB = other,
							chanceToFindA = await AsyncAskTryParse<float>($"Chance to find {location.name} from {other.name}?", float.TryParse),
							chanceToFindB = await AsyncAskTryParse<float>($"Chance to find {other.name} from {location.name}?", float.TryParse)
						};

						location.connections.Add(connection);
						other.connections.Add(connection);
					}

					return true;
				});
			}

			public override async Task Add()
			{
				Location location = new Location();

				location.name = await AsyncAsk("What is the name of the location?");
				
				await AsyncAskListBasic("Provide a description for the location.", 1, location.descriptions.Add);

				await AsyncAddConnections(location);

				await AsyncAskList("Name the ExploreSet.", 0, async name =>
				{
					ExploreSet set = ExploreSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An ExploreSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						location.exploreSet = set;

						return false;
					}
				});

				ChatCraft.Instance.State.locations.Add(location);

				ChatCraft.Instance.Save();

				await ReplyMentionAsync($"Finished adding {location.name}.");
			}

			public override async Task View(Location target)
			{
				string response = target.name + '\n';

				foreach (string description in target.descriptions)
				{
					response += '\n' + description;
				}

				response += '\n';

				foreach (Connection connection in target.connections)
				{
					response += $"\n{connection.locationA.name} {connection.locationB.name} {connection.chanceToFindA} {connection.chanceToFindB}";
				}

				response += $"\n{target.exploreSet?.name}";
				
				await ReplyMentionAsync(response);
			}

			public override async Task Edit(Location target)
			{
				await AsyncAskList("What should the name change to?", 0, name =>
				{
					target.name = name;
					return Task.FromResult(false);
				});

				string message = "These are the current descriptions:";

				foreach (string description in target.descriptions)
				{
					message += '\n' + description;
				}

				await ReplyMentionAsync(message);

				await AsyncRemove("Which descriptions should be removed?", target.descriptions, text => text, (start, full) => full.ToLower().StartsWith(start.ToLower()), removing => target.descriptions.Remove(removing));

				await AsyncAskListBasic("Provide a description for the location.", 1, target.descriptions.Add);

				await AsyncRemove("Which connections should be removed?", target.connections, Location.Find, (location, connection) => connection.locationA == location || connection.locationB == location, removing =>
				{
					removing.locationA.connections.Remove(removing);
					removing.locationB.connections.Remove(removing);

					target.connections.Remove(removing);
				});

				await ReplyMentionAsync($"Finished editing {target.name}.");

				await AsyncAddConnections(target);

				await AsyncAskList("Name the new ExploreSet.", 0, async name =>
				{
					if (name == ".")
					{
						target.exploreSet = null;
					}

					ExploreSet set = ExploreSet.Find(name);

					if (set == null)
					{
						await ReplyMentionAsync($"An ExploreSet of the name '{name}' could not be found.");

						return true;
					}
					else
					{
						target.exploreSet = set;

						return false;
					}
				});
			}

			public override async Task Remove(Location target)
			{
				if (await AsyncAskTryParse<bool>("You want to delete? (true / false)", bool.TryParse))
				{
					ChatCraft.Instance.State.locations.Remove(target);

					foreach (Location location in ChatCraft.Instance.State.locations)
					{
						location.connections.RemoveAll(item => item.locationA == target || item.locationB == target);
					}
				}
			}
		}
	}

	[Group("Go"), Alias("Location")]
	public class ChatCraftTravel : CrierModuleBase
	{
		public class LootOpportunity
		{
			public Player player;
			public Party party;
			public Location location;
			public ItemCount item;
		}

		public static Dictionary<ulong, LootOpportunity> loots = new Dictionary<ulong, LootOpportunity>();

		public static Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>> exploreResponses = new Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>>()
		{
			{
				Emojis.Repeat, async (reaction, context) =>
				{
					if (reaction.User.IsSpecified)
					{
						CustomContext customContext = new CustomContext(context)
						{
							User = reaction.User.Value
						};

						Player player = GetPlayer(customContext.User);

						if (player.party != null)
						{
							await Program.ExecuteCommand("party explore", customContext);
						}
						else
						{
							await Program.ExecuteCommand("go explore", customContext);
						}
					}

					return false;
				}
			},

			{
				Emojis.Gem, async (reaction, context) =>
				{
					if (reaction.User.IsSpecified && reaction.Message.IsSpecified)
					{
						CustomContext customContext = new CustomContext(context)
						{
							User = reaction.User.Value
						};

						LootOpportunity loot;

						ulong message = reaction.MessageId;

						if (loots.TryGetValue(message, out loot))
						{
							Player player = GetPlayer(customContext.User);

							Console.WriteLine(customContext.Message.CreatedAt + " " + DateTimeOffset.Now);

							if (player != loot.player && (loot.party == null || !loot.party.AllUnits.Contains(player)) && (customContext.Message.CreatedAt - DateTimeOffset.Now).TotalMinutes < 10)
							{
								await customContext.Channel.SendMessageAsync($"{customContext.User.Mention} - {loot.player?.name ?? loot.party.SafeName()} has 10 minutes to grab the loot before you can!");
							}
							else if ((player.party?.location ?? player.currentLocation) != loot.location)
							{
								await customContext.Channel.SendMessageAsync($"{customContext.User.Mention} - You must be at {loot.location.name} to grab the loot!");
							}
							else
							{
								loots.Remove(message);

								player.AddItem(loot.item.item, loot.item.count);

								RestUserMessage editing = await context.Channel.GetMessageAsync(message) as RestUserMessage;

								Console.WriteLine(editing.GetType());

								await editing.ModifyAsync(existing => existing.Content = $"{customContext.User.Mention} grabbed the {loot.item.item.name} (x{loot.item.count})!");
							}
						}
					}

					return false;
				}
			}
		};

		[Command("list")]
		public async Task List()
		{
			Player player = GetPlayer();

			if (player.locations.Count == 0)
			{
				await ReplyMentionAsync("You don't know any locations.");
				return;
			}

			string message = "You know the following locations:\n";

			foreach (Location location in player.locations)
			{
				message += "- " + location.name + "\n";
			}

			await ReplyMentionBlockAsync(message);
		}

		[Command, Alias("Where")]
		public async Task Where()
		{
			Player player = GetPlayer();

			await ReplyMentionAsync("You are at " + ((player.currentLocation == null) ? "Nowhere" : player.currentLocation.name));
		}

		[Command("To")]
		public async Task To([Found]Location location)
		{
			Player player = GetPlayer();

			if (player.currentLocation == location)
			{
				await ReplyMentionAsync($"You are already at {location.name}.");
			}
			else
			{
				player.currentLocation = location;

				await ReplyMentionAsync($"You travelled to {location.name}.");
			}
		}

		[Command("Explore")]
		public async Task Explore()
		{
			Player player = GetPlayer();

			if (player.party != null)
			{
				await ReplyMentionAsync("You must leave your party to explore alone!");
				return;
			}

			Location current = player.currentLocation;

			if (current == null)
			{
				current = ChatCraft.Instance.State.locations[0];
				player.currentLocation = current;
			}

			float luck = player.GetLuck();

			Encounter encounter;
			Location location;
			ItemCount item;

			string message = Explore("You", current, luck, player.locations.Contains, out encounter, out location, out item);
			
			if (location != null)
			{
				player.locations.Add(location);
			}

			if (encounter != null)
			{

			}
			
			IUserMessage response = await ReplyMentionBlockAsync(message);

			if (item != null)
			{
				LootOpportunity loot = new ChatCraftTravel.LootOpportunity()
				{
					item = item,
					player = player,
					location = player.party?.location ?? player.currentLocation
				};

				loots.Add(response.Id, loot);

				await response.AddReactionAsync(Emojis.Gem);
			}
			
			Criteria<SocketReaction> criteria = new Criteria<SocketReaction>();

			EmojiResponse emojiResponse = new EmojiResponse(Context, criteria, exploreResponses, TimeSpan.FromMinutes(10));

			await EmojiOption(response, emojiResponse, TimeSpan.FromMinutes(10), Emojis.Repeat);
		}

		public static string Explore(string name, Location location, float luck, Predicate<Location> isKnown, out Encounter bestEncounter, out Location bestLocation, out ItemCount bestItem)
		{ 
			Random random = new Random();

			double bestEncounterWeight = 1f;
			bestEncounter = null;

			location.exploreSet?.encounterSet?.GetBest(random, ref bestEncounter, ref bestEncounterWeight);

			double bestLocationWeight = bestEncounterWeight;
			bestLocation = null;

			foreach (Connection connection in location.connections)
			{
				bool isA = connection.locationA == location;

				Location other = isA ? connection.locationB : connection.locationA;
				float chance = isA ? connection.chanceToFindB : connection.chanceToFindA;

				if (!isKnown(other))
				{
					double value = random.NextDouble() / chance;

					if (value < bestLocationWeight)
					{
						bestLocation = other;
						bestLocationWeight = value;
					}
				}
			}

			double bestItemWeight = bestEncounterWeight;
			ItemWeightCount bestItemWeightCount = null;

			bestItem = null;
			
			location.exploreSet?.itemSet?.GetBest(random, ref bestItemWeightCount, ref bestEncounterWeight);

			if (bestItemWeightCount != null)
			{
				bestItem = new ItemCount(bestItemWeightCount.item, random.Next(bestItemWeightCount.minimum, bestItemWeightCount.maximum + 1));
			}

			string message = $"{name} explore {location.name}.\n";

			if (location.descriptions.Count > 0)
			{
				message += location.descriptions[random.Next(location.descriptions.Count)] + "\n";
			}

			message += "\n";

			if (bestItem != null)
			{
				bestEncounter = null;
				bestLocation = null;
				
				if (bestItem.count > 1)
				{
					message += $"You found {bestItem.count} {bestItem.item.name}s.";
				}
				else
				{
					message += $"You found a {bestItem.item.name}.";
				}
			}
			else if (bestLocation != null)
			{
				bestEncounter = null;
				
				message += $"{name} discovered {bestLocation.name}.";
			}
			else if (bestEncounter != null)
			{
				message += $"{name} encountered a {bestEncounter.name}.\n";
				message += $"{bestEncounter.possibleDescriptions[random.Next(bestEncounter.possibleDescriptions.Count)]}\n";
				message += $"";
			}
			else
			{
				message += $"{name} didn't find anything!";
			}

			return message;
		}
	}

	[Group("Coins"), Alias("Coin", "Gold", "Money", "Cash")]
	public class ChatCraftCoins : CrierModuleBase
	{
		[Command]
		public async Task Coins()
		{
			Player player = GetPlayer();

			await ReplyMentionAsync("You have " + player.coins + " coins.");
		}

		[Command("Pay")]
		public async Task Pay(IUser target, int amount)
		{
			Player player = GetPlayer();

			if (amount > player.coins)
			{
				await ReplyMentionAsync($"You don't have {amount} coins. You only have {player.coins}.");
			}
			else
			{
				Player other = GetPlayer(target);
				other.coins += amount;
				player.coins -= amount;

				await ReplyMentionAsync($"You gave {target.Mention} {amount} coins.");
			}
		}

		[Command("Make it rain"), Alias("Rain")]
		public async Task Rain(int amount)
		{
			Player player = GetPlayer();

			if (amount > player.coins)
			{
				await ReplyMentionAsync($"You don't have {amount} coins. You only have {player.coins}.");
			}
			else
			{
				IAsyncEnumerable<IReadOnlyCollection<IMessage>> result = Context.Channel.GetMessagesAsync();

				List<IUser> users = new List<IUser>((await result.FlattenAsync()).Select(item => item.Author).Distinct());

				users.Remove(Context.User);

				if (users.Count == 0)
				{
					await ReplyMentionAsync("There's no one else around!");
				}
				
				if (users.Count > amount)
				{
					users.RemoveRange(amount, users.Count - amount);
				}

				int perPerson = amount / users.Count;

				int remainder = amount - perPerson * users.Count;

				player.coins -= amount;

				for (int i = 0; i < users.Count; i++)
				{
					int count = perPerson + (i < remainder ? 1 : 0);

					GetPlayer(users[i]).coins += count;
				}
				
				await ReplyMentionAsync($"You threw {amount} coins in the air!\nThese {users.Count} people caught some, each getting ~{perPerson}:\n- {string.Join("\n -", users.Select(item => (item is IGuildUser) ? ((IGuildUser)item).Nickname ?? item.Username : item.Username))}");
			}
		}

		//TODO: Help
	}

	[Group("Party")]
	public class ChatCraftParty : CrierModuleBase
	{
		static Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>> inviteResponses = new Dictionary<IEmote, Func<SocketReaction, SocketCommandContext, Task<bool>>>()
		{
			{
				Emojis.Tick, async (reaction, context) =>
				{
					if (reaction.User.IsSpecified)
					{
						IUser author = context.User;

						CustomContext customContext = new CustomContext(context)
						{
							User = reaction.User.Value
						};

						string command = $"party join {author.Mention}";

						Console.WriteLine(command);

						await Program.ExecuteCommand(command, customContext);
					}

					return false;
				}
			}
		};

		[Command]
		public async Task Current()
		{
			Player player = GetPlayer();
			
			if (player.party == null)
			{
				await ReplyMentionAsync("You are currently not in a party.");
			}
			else
			{
				await ReplyMentionBlockAsync(player.party.ToString());
			}
		}

		[Command("Create")]
		public async Task Create(string name = null)
		{
			Player player = GetPlayer();

			if (player.party != null)
			{
				await ReplyMentionAsync("You are already in a party!");
			}
			else
			{
				Party party = new Party();

				party.name = name;
				party.location = player.currentLocation;

				ChatCraft.Instance.State.parties.Add(party);
				
				party.Add(player);

				await ReplyMentionAsync($"{party.SafeName(true)} has been created!");
			}
		}

		[Command("Invite")]
		public async Task Invite(IUser targetUser)
		{
			Player player = GetPlayer();
			Player target = GetPlayer(targetUser);

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else if (target.party == player.party)
			{
				await ReplyMentionAsync($"{targetUser.Mention} is already in {player.party.SafeName()}!");
			}
			else if (player.party.invited.Contains(target))
			{
				await ReplyMentionAsync($"{targetUser.Mention} is already invited!");
			}
			else if (player.party.invited.Count + player.party.currentUnits.Count + player.party.deadUnits.Count + player.party.fledUnits.Count > Party.MaximumUnits)
			{
				await ReplyMentionAsync($"{player.party.SafeName(true)} already has {Party.MaximumUnits} joined or invited members.");
			}
			else
			{
				player.party.invited.Add(target);

				IUserMessage message = await ReplyMentionAsync($"{targetUser.Mention} has been invited to join {player.party.SafeName()}!");

				Criteria<SocketReaction> criteria = new Criteria<SocketReaction>();
				
				EmojiResponse emojiResponse = new EmojiResponse(Context, criteria, inviteResponses, TimeSpan.FromMinutes(10));

				await EmojiOption(message, emojiResponse, TimeSpan.FromMinutes(10), Emojis.Tick);
			}
		}

		[Command("Join"), Alias("Accept")]
		public async Task Join(IUser targetUser)
		{
			Player player = GetPlayer();
			Player target = GetPlayer(targetUser);

			if (player.party != null)
			{
				await ReplyMentionAsync("You are already in a party!");
			}
			else if (target.party == null)
			{
				await ReplyMentionAsync($"{targetUser.Mention} is not in a party!");
			}
			else if (!target.party.invited.Contains(player))
			{
				await ReplyMentionAsync($"{targetUser.Mention} must invite you first!");
			}
			else if (target.party.location != player.currentLocation)
			{
				await ReplyMentionAsync($"You must be at {target.party.location.name} to join {target.party.SafeName()}!");
			}
			else
			{
				target.party.invited.Remove(player);

				target.party.Add(player);

				if (target.party.name == null)
				{
					await ReplyMentionAsync($"You have joined {targetUser.Mention}'s party!");
				}
				else
				{
					await ReplyMentionAsync($"You have joined {targetUser.Mention} in '{player.party.name}'");
				}
			}
		}

		[Command("Leave")]
		public async Task Leave()
		{
			Player player = GetPlayer();

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else
			{
				Party party = player.party;

				party.currentUnits.Remove(player);
				party.deadUnits.Remove(player);
				party.fledUnits.Remove(player);

				if (party.currentUnits.Count + party.deadUnits.Count + party.fledUnits.Count == 0)
				{
					ChatCraft.Instance.State.parties.Remove(party);
				}

				player.party = null;

				player.currentLocation = party.location;

				await ReplyMentionAsync($"You have left {party.SafeName()}");
			}
		}

		[Command("Uninvite")]
		public async Task Uninvite(IUser targetUser)
		{
			Player player = GetPlayer();
			Player target = GetPlayer(targetUser);

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else if (target.party == player.party)
			{
				await ReplyMentionAsync($"{targetUser.Mention} is already in your party! Use `kick` to remove them.");
			}
			else if (!player.party.invited.Contains(target))
			{
				await ReplyMentionAsync($"{targetUser.Mention} does not have an invitation!");
			}
			else
			{
				player.party.invited.Remove(target);

				await ReplyMentionAsync($"{targetUser.Mention}'s invitation to {player.party.SafeName()} has been revoked!");
			}
		}

		[Command("Kick")]
		public async Task Kick(IUser targetUser)
		{
			Player player = GetPlayer();
			Player target = GetPlayer(targetUser);

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else if (player.party.invited.Contains(target))
			{
				await ReplyMentionAsync($"Use `uninvite` to revoke {targetUser.Mention}'s invitation.");
			}
			else if (target.party != player.party)
			{
				await ReplyMentionAsync($"{targetUser.Mention} is not in your party!");
			}
			else
			{
				player.party.currentUnits.Remove(target);
				player.party.deadUnits.Remove(target);
				player.party.fledUnits.Remove(target);

				target.currentLocation = player.party.location;

				target.party = null;

				await ReplyMentionAsync($"{targetUser.Mention} has been kicked from {player.party.SafeName()}!");
			}
		}
		
		[Command("Name")]
		public async Task Name(string name = null)
		{
			Player player = GetPlayer();

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else
			{
				string oldName = player.party.SafeName();

				player.party.name = name;

				string newName = player.party.SafeName();
								
				if (oldName == newName)
				{
					await ReplyMentionAsync($"{oldName}'s name has not changed.");
				}

				if (name == null)
				{
					await ReplyMentionAsync($"{oldName} no longer has a name.");
				}
				else
				{
					await ReplyMentionAsync($"{oldName} has been renamed to '{newName}'!");
				}
			}
		}

		[Command("Invited")]
		public async Task Invited()
		{
			Player player = GetPlayer();
				
			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else if (player.party.invited.Count > 0)
			{
				await ReplyMentionBlockAsync(player.party.InviteList());
			}
			else
			{
				await ReplyMentionAsync("There are no pending invites.");
			}
		}


		[Command, Alias("Where")]
		public async Task Where()
		{
			Player player = GetPlayer();

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else
			{
				await ReplyMentionAsync($"{player.party.SafeName()} is at " + ((player.party.location == null) ? "Nowhere" : player.party.location.name));
			}
		}

		[Command("To")]
		public async Task To([Found]Location location)
		{
			Player player = GetPlayer();

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
			}
			else
			{
				if (player.party.location == location)
				{
					await ReplyMentionAsync($"You are already at {location.name}.");
				}
				else
				{
					player.party.location = location;

					string response = $"{player.party.SafeName()} travelled to {location.name}.";
					
					foreach (Unit unit in player.party.AllUnits)
					{
						Player member = unit as Player;

						if (member != null && !member.locations.Contains(location))
						{
							member.locations.Add(location);

							response += $"\n{member.name} discovered a new location!";
						}
					}

					await ReplyMentionAsync(response);
				}
			}
		}

		[Command("Explore")]
		public async Task Explore()
		{
			Player player = GetPlayer();

			if (player.party == null)
			{
				await ReplyMentionAsync("You are not in a party!");
				return;
			}

			Location current = player.party.location;

			float luck = player.GetLuck();

			Encounter encounter;
			Location location;
			ItemCount item;

			Predicate<Location> allKnowLocation = test =>
			{
				return player.party.AllUnits.All(unit => !(unit is Player) || ((Player)unit).locations.Contains(test));
			};

			string message = ChatCraftTravel.Explore(player.party.SafeName(), current, luck, allKnowLocation, out encounter, out location, out item);
			
			if (location != null)
			{
				foreach (Unit unit in player.party.AllUnits)
				{
					Player member = unit as Player;

					if (member != null && !member.locations.Contains(location))
					{
						member.locations.Add(location);

						message += $"\n{member.name} discovered a new location!";
					}
				}
			}

			if (encounter != null)
			{

			}

			IUserMessage response = await ReplyMentionBlockAsync(message);

			if (item != null)
			{
				ChatCraftTravel.LootOpportunity loot = new ChatCraftTravel.LootOpportunity()
				{
					item = item,
					party = player.party,
					location = player.party.location
				};

				await response.AddReactionAsync(Emojis.Gem);

				ChatCraftTravel.loots.Add(response.Id, loot);
			}

			Criteria<SocketReaction> criteria = new Criteria<SocketReaction>();

			EmojiResponse emojiResponse = new EmojiResponse(Context, criteria, ChatCraftTravel.exploreResponses, TimeSpan.FromMinutes(10));

			await EmojiOption(response, emojiResponse, TimeSpan.FromMinutes(10), Emojis.Repeat);
		}


		[Command("Help")]
		public async Task Help()
		{
			string prefix = "!party ";

			List<string> commands = new List<string>();
			List<string> descriptions = new List<string>();

			string message = $"This is the party system! A party allows you to work with others in your adventures!\nCurrently, parties are limited to {Party.MaximumUnits} members.\n\n";

			commands.Add("");
			descriptions.Add("View your current party status");
			commands.Add("create [name]");
			descriptions.Add("Creates a new party, with an optional name");
			commands.Add("invite [player]");
			descriptions.Add("Invites a player to your party");
			commands.Add("uninvite [player]");
			descriptions.Add("Uninvites a player");
			commands.Add("join [player]");
			descriptions.Add("Joins another players party");
			commands.Add("leave");
			descriptions.Add("Leaves your current party");
			commands.Add("kick");
			descriptions.Add("Kicks a player from your party");
			commands.Add("name [new name]");
			descriptions.Add("Renames your party");
			commands.Add("invited");
			descriptions.Add("Lists players invited to your party");
			commands.Add("[go command]");
			descriptions.Add("Does the 'go' command for your party (eg. explore, travel, etc.)");

			message += ShowCommands(prefix, commands, descriptions);

			await ReplyAsync(message);
		}
	}
}

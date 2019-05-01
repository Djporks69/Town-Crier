using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.ChatCraft
{
	public class RelatedCommands : CrierModuleBase
	{
		static Random random = new Random();

		[Command("flip"), Alias("heads or tails", "flip a coin")]
		public async Task FlipCoin()
		{
			Player player = GetPlayer();

			await Task.Run(async () =>
			{
				if (player.coins > 0)
				{
					if (random.NextDouble() < 0.01f)
					{
						await ReplyAsync("The coin is rolling away!");

						await Task.Delay(1000);

						if (random.NextDouble() > 0.5f)
						{
							await ReplyAsync("No wait, it's falling over...");

							await Task.Delay(500);
						}
						else
						{
							await ReplyAsync("It's gone!");
							return;
						}
					}

					await ReplyAsync(random.Next(2) == 0 ? "<:Coin2:555532079312666628> Heads!" : "<:Coin:555532103136051200> Tails!");
				}
				else
				{
					await ReplyAsync("You don't have any coins!");
				}
			});
		}

		[Command("fIip")]
		public async Task FlipCoinWeighted()
		{
			Player player = GetPlayer();

			if (player.coins > 0)
			{
				await ReplyAsync("<:Coin2:555532079312666628> Heads!");
			}
			else
			{
				await ReplyAsync("You don't have any coins!");
			}
		}

		[Command("roll"), Alias("d6", "dice", "roll a dice")]
		public async Task RollDice()
		{
			Player player = GetPlayer();

			if (player.items.Any(count => count.item.name == "Dice"))
			{
				await ReplyAsync("You rolled a " + (random.Next(6) + 1));
			}
			else
			{
				await ReplyAsync("You don't have any dice");
			}
		}

		[Command("testscore")]
		public async Task Score(IUser user = null)
		{
			Player player = GetPlayer(user ?? Context.User);

			await ReplyAsync(player.name + " has a score of: " + player.score);
		}

		[Command("testtop")]
		public async Task TestTop(int count)
		{
			foreach (Player player in ChatCraft.Instance.State.players.OrderByDescending(item => item.score).Take(count))
			{
				SocketGuildUser user = Context.Guild.GetUser(player.identifier);
				
				await ReplyAsync((user.Nickname ?? user.Username) + " : " + player.score);
			}
		}
	}

	[Group("town"), Alias("tc")]
	public class TownCommands : CrierModuleBase
	{
		[Command(), Priority(-10000)]
		public async Task Catch(string command)
		{
			await ReplyAsync($"I don't know what '{command}' means. Feel free to ask for help! ('!tc help')");
		}

		[Command("save", RunMode = RunMode.Async), RequireAdmin]
		public async Task Save()
		{
			ChatCraft.Instance.Save();

			AccountModule.Save();

			await ReplyMentionAsync("Saved!");
		}

		[Command("quit"), Alias("exit"), RequireAdmin]
		public async Task Quit()
		{
			Player player = GetPlayer();

			ChatCraft.Instance.Save();
			await ReplyAsync("Saved! Quitting...");

			Environment.Exit(0);
		}

		[Group("getstarted"), Alias("help", "gettingstarted")]
		public class Help : CrierModuleBase
		{
			[Command]
			public async Task GetStarted()
			{
				string prefix = "!tc ";

				List<string> commands = new List<string>();
				List<string> descriptions = new List<string>();

				string message = $"Welcome to A Chatty Township Tale! I am the Town Crier, your Game Master.\n\nLet me tell you about some some useful commands:\n\n";

				commands.Add("where");
				descriptions.Add("View your location");
				commands.Add("health");
				descriptions.Add("View your health");
				commands.Add("coins");
				descriptions.Add("View your money");
				commands.Add("equipment");
				descriptions.Add("View all equipped items");
				commands.Add("inventory");
				descriptions.Add("View equipped, and unequipped items");
				commands.Add("[slot]");
				descriptions.Add("View equipment slot (see list below)");
				commands.Add("equip [slot] [item]");
				descriptions.Add("Equip an item to a slot");
				commands.Add("unequip [slot]");
				descriptions.Add("Unequip a slot");
				commands.Add("give [item] [count] [player]");
				descriptions.Add("Give an item to another player");
				commands.Add("pay [count] [player]");
				descriptions.Add("Give coins to another player");

				message += ShowCommands(prefix, commands, descriptions);

				await ReplyAsync(message);
			}
		}

		#region Inventory
		

		[Command("give")]
		public async Task Give(IUser user, int count, [ InInventory]Item item)
		{
			Player player = GetPlayer();

			ItemCount itemCount = player.items.FirstOrDefault(test => test.item == item);

			if (count > itemCount.count)
			{
				await ReplyAsync($"You don't have {count} {itemCount.item.name}. You only have {itemCount.count}.");
			}
			else
			{
				Player other = GetPlayer(user);

				if (itemCount.item.itemType == ItemType.Armor ||
					itemCount.item.itemType == ItemType.Tool)
				{
					other.items.Add(itemCount);
					player.items.Remove(itemCount);
				}
				else
				{
					other.AddItem(itemCount.item, count);
					player.ConsumeItem(itemCount.item, count);
				}

				await ReplyAsync($"You gave {user.Username} {count} {itemCount.item.name}.");
			}
		}


		[Command("stats")]
		public async Task Stats()
		{
			Player player = GetPlayer();

			string message = $"Your stats are:\n";

			foreach (KeyValuePair<Stat, Player.StatGroup> pair in player.GetStats())
			{
				string handsValue = (pair.Value.left + pair.Value.right > 0) ? $"(+{pair.Value.right}) (+{pair.Value.left})" : "";

				message += $"{pair.Key.name} : {pair.Value.shared} {handsValue}\n";
			}

			await ReplyAsync(message);
		}

		[Command, Priority(-1)]
		public async Task GetStat(Stat stat)
		{
			Player player = GetPlayer();

			Player.StatGroup stats = player.GetStat(stat);

			string handsValue = (stats.left + stats.right > 0) ? $"(+{stats.right}) (+{stats.left})" : "";

			await ReplyAsync($"You have {stats.shared} {handsValue} {stat.name}.");
		}
		
		[Group]
		public class EquipCommands : CrierModuleBase
		{
			[Command, Priority(-3)]
			public async Task GetSlot(Slot slot)
			{
				Player player = GetPlayer();

				if (!player.equipped.ContainsKey(slot))
				{
					player.equipped.Add(slot, null);
				}

				string equipped = player.equipped[slot] == null ? $"no {slot.names[0]}" : player.equipped[slot].item.name;

				await ReplyAsync($"You have {equipped} equipped.");
			}


			[Command("equip")]
			public async Task Equip(Slot slot, [ItemTypeSlot, InInventory]Item item)
			{
				Player player = GetPlayer();

				if (!player.equipped.ContainsKey(slot))
				{
					player.equipped.Add(slot, null);
				}

				ItemCount itemCount = player.items.FirstOrDefault(test => test.item == item);

				string message = "";

				ItemCount equipped = player.equipped[slot];

				if (equipped != null && equipped.item != null)
				{
					message += $"You unequipped your {equipped.item.name}.\n";
					player.ReturnEquipment(equipped);
					player.equipped[slot] = null;
				}

				message += $"You have equipped {itemCount.item.name}.";

				player.equipped[slot] = player.TakeEquipment(itemCount);

				await ReplyAsync(message);
			}

			[Command("unequip")]
			public async Task Unequip(Slot slot)
			{
				Player player = GetPlayer();

				if (!player.equipped.ContainsKey(slot))
				{
					player.equipped.Add(slot, null);
				}

				ItemCount equipped = player.equipped[slot];

				if (equipped == null)
				{
					await ReplyAsync($"You have no {slot.names[0]} equipped.");
				}
				else
				{
					await ReplyAsync($"You have unequipped {equipped.item.name}.");

					player.ReturnEquipment(equipped);
					player.equipped[slot] = null;
				}
			}
		}

		[Command("lookat"), Alias("Examine", "look")]
		public async Task LookAtItem([ InInventoryOrEquipment]Item item)
		{
			Player player = GetPlayer();

			string message = $"**{item.name}**\n{item.itemType}\n{item.description}\n";

			foreach (StatCount statCount in item.statModifications)
			{
				message += $"{statCount.stat.name} : {statCount.count}\n";
			}

			await ReplyAsync(message);
		}

		[Command("equipment")]
		public async Task GetEquipment()
		{
			Player player = GetPlayer();

			string message = GetEquipmentMessage(player);

			await ReplyAsync(message);
		}

		public static string GetEquipmentMessage(Player player)
		{
			List<Item> equipped = new List<Item>();

			foreach (KeyValuePair<Slot, ItemCount> equipment in player.equipped)
			{
				if (equipment.Value != null)
				{
					equipped.Add(equipment.Value.item);
				}
			}

			string message;

			if (equipped.Count == 0)
			{
				message = "You have nothing equipped.\n";
			}
			else if (equipped.Count == 1)
			{
				message = $"You have a {equipped[0].name} equipped.\n";
			}
			else
			{
				message = "You have ";

				for (int i = 0; i < equipped.Count - 1; i++)
				{
					message += $"{equipped[i].name}, ";
				}

				message += $"and {equipped[equipped.Count - 1].name} equipped.\n";
			}

			return message;
		}

		[Group("inventory"), Alias("items")]
		public class InventoryCommands : CrierModuleBase
		{
			[Command(), Alias("all")]
			public async Task GetInventory()
			{
				Player player = GetPlayer();

				string message = GetEquipmentMessage(player);

				if (player.items.Count == 0)
				{
					message += "You have nothing in your inventory.\n";
				}
				else
				{
					message += "In your inventory, you have: \n";

					int count = 0;

					foreach (ItemCount itemCount in player.items)
					{
						if (itemCount.item.itemType == ItemType.Armor || itemCount.item.itemType == ItemType.Tool)
						{
							//Count is durability
							message += $"- {itemCount.item.name} ({itemCount.count})\n";
						}
						//else if (itemCount.item.itemType == ItemType.Ring || itemCount.item.itemType == ItemType.Pendant)
						//{
						//	//Count is non existant
						//	message += $"- {itemCount.item.name}\n";
						//}
						else
						{
							//Count is literal count
							message += $"- {itemCount.item.name} x{itemCount.count}\n";
						}

						count++;

						if (count % 50 == 0)
						{
							await ReplyAsync(message);
							message = "";
						}
					}
				}

				await ReplyAsync(message);
			}
		}

		#endregion
		
		#region Recipes

		[Group("recipes"), Alias("recipe", "task", "activity")]
		public class RecipeCommandsOptional : CrierModuleBase
		{
			[Command(), Priority(-3)]
			public async Task Craft(int count, [ Learnt]Recipe recipe)
			{
				Player player = GetPlayer();

				if (recipe.allowedLocations.Count > 0)
				{
					if (!recipe.allowedLocations.Contains(player.currentLocation))
					{
						await ReplyAsync($"You can't craft {recipe.name} in your current location.");
						return;
					}
				}

				if (recipe.requiredStatCount.Count > 0)
				{
					Dictionary<Stat, Player.StatGroup> stats = player.GetStats();

					foreach (StatCount requiredStat in recipe.requiredStatCount)
					{
						Console.WriteLine(requiredStat.stat);

						if (stats[requiredStat.stat].Total < requiredStat.count)
						{
							if (requiredStat.stat.isTool)
							{
								await ReplyAsync($"You need to equip a {requiredStat.stat.name} of efficiency {requiredStat.count} to craft {recipe.name}.");
							}
							else
							{
								await ReplyAsync($"You need at least {requiredStat.count} {requiredStat.stat.name} to craft {recipe.name}.");
							}
							return;
						}
					}
				}

				int canMake = int.MaxValue;

				for (int i = 0; i < recipe.consumed.Count; i++)
				{
					canMake = Math.Min(canMake, player.ItemCount(recipe.consumed[i].item) / recipe.consumed[i].count);
				}

				if (canMake == 0)
				{
					await ReplyAsync($"You don't have enough resources to craft even one {recipe.name}");
					return;
				}

				if (canMake < count)
				{
					await ReplyAsync($"You don't have enough resources to craft {count} of {recipe.name}.\n You only have enough for {canMake}.");
					return;
				}

				for (int i = 0; i < recipe.consumed.Count; i++)
				{
					player.ConsumeItem(recipe.consumed[i].item, recipe.consumed[i].count * count);
				}

				Random random = new Random();

				if (random.NextDouble() < recipe.successChance)
				{
					for (int i = 0; i < recipe.creates.Count; i++)
					{
						player.AddItem(recipe.creates[i].item, recipe.creates[i].count * count);
					}

					if (count > 1)
					{
						await ReplyAsync($"You have crafted {count} {recipe.name}.");
					}
					else
					{
						await ReplyAsync($"You have crafted {recipe.name}.");
					}
				}
				else
				{
					await ReplyAsync("No success!");
				}
			}

			[Command(), Priority(-3)]
			public async Task Craft([ Learnt]Recipe recipe)
			{
				await Craft(1, recipe);
			}
		}

		[Group("recipe"), Alias("task", "activity")]
		public class RecipeCommands : CrierModuleBase
		{
			[Command("Read"), Alias("Look", "Lookat")]
			public async Task Read([ Learnt]Recipe recipe)
			{
				Player player = GetPlayer();

				string message = $"{recipe.name}\n";

				if (recipe.description != null && recipe.description.Length > 0)
				{
					message += recipe.description + "\n";
				}

				if (recipe.successChance < 1f)
				{
					message += $"\n**Success Rate:** {recipe.successChance * 100}%\n";
				}

				if (recipe.allowedLocations.Count > 0)
				{
					message += "\n**Can be done at:**\n";

					bool isUnknown = false;
					int foundCount = 0;
					string last = null;

					foreach (Location location in recipe.allowedLocations)
					{
						if (player.locations.Contains(location))
						{
							if (last != null)
							{
								message += last + ", ";
							}

							last = location.name;

							foundCount++;
						}
						else
						{
							isUnknown = true;
						}
					}

					if (isUnknown)
					{
						if (last != null)
						{
							message += last + ", or unknown.";
						}
						else
						{
							message += "unkown.";
						}
					}
					else if (last != null)
					{
						if (foundCount > 1)
						{
							message += $"and {last}.";
						}
						else
						{
							message += last + ".";
						}
					}

					message += "\n";
				}
				else
				{
					message += "\nCan be done anywhere.\n";
				}

				if (recipe.requiredStatCount.Count > 0)
				{
					message += "\n**Requires:**\n";

					string last = null;

					foreach (StatCount stat in recipe.requiredStatCount)
					{
						if (last != null)
						{
							message += last + ", ";
						}

						last = stat.stat.name + " >= " + stat.count;
					}

					if (recipe.requiredStatCount.Count > 1)
					{
						message += $"or {last}.";
					}
					else
					{
						message += $"{last}.";
					}

					message += "\n";
				}

				if (recipe.requiredTools.Count > 0)
				{
					message += "\n**Requires:**\n";

					string last = null;

					foreach (Item item in recipe.requiredTools)
					{
						if (last != null)
						{
							message += last + ", ";
						}

						last = item.name;
					}

					if (recipe.requiredTools.Count > 1)
					{
						message += $"or {last}.";
					}
					else
					{
						message += $"{last}.";
					}

					message += "\n";
				}

				int count = int.MaxValue;

				if (recipe.consumed.Count > 0)
				{
					message += "\n**Costs:**\n";

					for (int i = 0; i < recipe.consumed.Count; i++)
					{
						message += $"{recipe.consumed[i].count} x {recipe.consumed[i].item.name}\n";

						count = Math.Min(count, player.ItemCount(recipe.consumed[i].item) / recipe.consumed[i].count);
					}
				}

				message += $"\n**Produces:**\n";

				for (int i = 0; i < recipe.creates.Count; i++)
				{
					message += $"{recipe.creates[i].count} x {recipe.creates[i].item.name}\n";
				}

				if (count != int.MaxValue)
				{
					message += $"\nYou can make: {count}";
				}

				await ReplyAsync(message);
			}

			[Command("list")]
			public async Task ListPlayers()
			{
				Player player = GetPlayer();

				if (player.recipes.Count == 0)
				{
					await ReplyAsync("You don't know any recipes.");
					return;
				}

				string message = "You know the following recipes:\n";

				foreach (Recipe recipe in player.recipes)
				{
					message += "- " + recipe.name + "\n";
				}

				await ReplyAsync(message);
			}

			[Command("share with"), Alias("sharewith", "teach")]
			public async Task ShareWith(IUser user, [ Learnt]Recipe recipe)
			{
				Player player = GetPlayer();
				Player target = GetPlayer(user);

				if (target.recipes.Contains(recipe))
				{
					await ReplyAsync($"{user.Username} already knows {recipe.name}!");
				}
				else
				{
					target.recipes.Add(recipe);
					await ReplyAsync($"You taught {user.Username} {recipe.name}!");
				}
			}
		}


		#endregion

		[Group("debug"), Alias("admin", "d"), RequireAdmin]
		public class DebugCommands : CrierModuleBase
		{
			[Command("generalfix")]
			public async Task JoinFix()
			{
				await ChatCraft.Instance.ApplyFixes(Context.Guild);

				await ReplyAsync("Fixed");
			}

			[Command("sort")]
			public async Task Sort()
			{
				ChatCraftState state = ChatCraft.Instance.State;

				state.items.Sort((a, b) => a.name.CompareTo(b.name));
				state.recipes.Sort((a, b) => a.name.CompareTo(b.name));
				state.locations.Sort((a, b) => a.name.CompareTo(b.name));
				state.connections.Sort((a, b) =>
				{
					int result = a.locationA.name.CompareTo(b.locationA.name);

					if (result == 0)
					{
						result = a.locationB.name.CompareTo(b.locationB.name);
					}

					return result;
				});

				state.encounters.Sort((a, b) => a.name.CompareTo(b.name));
				state.stats.Sort((a, b) => a.name.CompareTo(b.name));

				await ReplyAsync("Everything sorted");
			}

			[Group("f")]
			public class Fix : CrierModuleBase
			{
				class Metal
				{
					public string name;
					public float damageMultiplier;
					public int durability;

					public Metal(string name, float damageMultiplier, int durability)
					{
						this.name = name;
						this.damageMultiplier = damageMultiplier;
						this.durability = durability;
					}
				}

				static Metal[] metals = new Metal[]
				{
					new Metal("Copper", 0.7f, 40),
					new Metal("Iron", 1f, 50),
					new Metal("Gold", 0.7f, 20),
					new Metal("Silver", 0.6f, 40),
					new Metal("Platinum", 1.5f, 40),
					new Metal("Mythril", 2f, 30)
				};

				[Command("metal items")]
				public async Task MetalItems(bool isReplacing = false)
				{
					Player player = GetPlayer();

					List<Item> newItems = new List<Item>();

					foreach (Item item in ChatCraft.Instance.State.items)
					{
						if (item.name.Contains("Iron"))
						{
							foreach (Metal metal in metals)
							{
								string newName = item.name.Replace("Iron", metal.name);

								Item found = ChatCraft.Instance.State.items.FirstOrDefault(test => test.name == newName);

								if (found == null)
								{
									found = new Item();
									found.name = newName;

									newItems.Add(found);
								}
								else if (!isReplacing)
								{
									continue;
								}

								found.itemType = item.itemType;

								found.description = item.description.Replace("Iron", metal.name);

								List<StatCount> newStatModifications = new List<StatCount>();

								foreach (StatCount statCount in item.statModifications)
								{
									int value = statCount.count;

									if (statCount.stat.name == "Damage" ||
										statCount.stat.name == "Armor" ||
										statCount.stat.name == "Axe" ||
										statCount.stat.name == "Hammer" ||
										statCount.stat.name == "Pick Axe")
									{
										value = (int)(metal.damageMultiplier * value + 0.5f);
									}

									newStatModifications.Add(new StatCount() { stat = statCount.stat, count = value });
								}

								found.statModifications = newStatModifications;

								if (found.itemType == ItemType.Tool || found.itemType == ItemType.Armor)
								{
									found.durability = metal.durability;
								}
							}
						}
					}

					ChatCraft.Instance.State.items.AddRange(newItems);

					await ReplyAsync("Fixed metal items");
				}

				[Command("metal recipes")]
				public async Task MetalRecipes(bool isReplacing = false)
				{
					Player player = GetPlayer();

					List<Recipe> newRecipes = new List<Recipe>();

					foreach (Recipe recipe in ChatCraft.Instance.State.recipes)
					{
						if (recipe.name.Contains("Iron"))
						{
							foreach (Metal metal in metals)
							{
								string newName = recipe.name.Replace("Iron", metal.name);

								Recipe found = ChatCraft.Instance.State.recipes.FirstOrDefault(test => test.name == newName);

								if (found == null)
								{
									found = new Recipe();
									found.name = newName;

									newRecipes.Add(found);
								}
								else if (!isReplacing)
								{
									continue;
								}

								found.allowedLocations = new List<Location>(recipe.allowedLocations);

								found.requiredTools = new List<Item>(recipe.requiredTools);

								List<StatCount> newRequiredStatCount = new List<StatCount>();

								foreach (StatCount required in recipe.requiredStatCount)
								{
									newRequiredStatCount.Add(new StatCount() { stat = required.stat, count = required.count });
								}

								found.requiredStatCount = newRequiredStatCount;

								if (recipe.description == null)
								{
									found.description = null;
								}
								else
								{
									found.description = recipe.description.Replace("Iron", metal.name);
								}

								List<ItemCount> newCreates = new List<ItemCount>();
								List<ItemCount> newConsumes = new List<ItemCount>();

								foreach (ItemCount creates in recipe.creates)
								{
									Item item = creates.item;

									if (item.name.Contains("Iron"))
									{
										item = ChatCraft.Instance.State.items.FirstOrDefault(test => test.name == item.name.Replace("Iron", metal.name));
									}

									newCreates.Add(new ItemCount(item, creates.count));
								}

								found.creates = newCreates;

								foreach (ItemCount consumes in recipe.consumed)
								{
									Item item = consumes.item;

									if (item.name.Contains("Iron"))
									{
										item = ChatCraft.Instance.State.items.FirstOrDefault(test => test.name == item.name.Replace("Iron", metal.name));
									}

									newConsumes.Add(new ItemCount(item, consumes.count));
								}

								found.consumed = newConsumes;
							}
						}
					}

					ChatCraft.Instance.State.recipes.AddRange(newRecipes);

					await ReplyAsync("Fixed metal recipes");
				}
			}

			[Group("l")]
			public class List : CrierModuleBase
			{
				[Command("items")]
				public async Task Items()
				{
					Player player = GetPlayer();

					string message = "";

					foreach (Item item in ChatCraft.Instance.State.items)
					{
						message += $"'{item.name}'\n";
					}

					await ReplyAsync(message);
				}

				[Command("recipes")]
				public async Task Recipes()
				{
					Player player = GetPlayer();

					string message = "";

					foreach (Recipe recipe in ChatCraft.Instance.State.recipes)
					{
						message += $"'{recipe.name}'\n";
					}

					await ReplyAsync(message);
				}

				[Command("locations")]
				public async Task Locations()
				{
					Player player = GetPlayer();

					string message = "";

					foreach (Location location in ChatCraft.Instance.State.locations)
					{
						message += $"'{location.name}'\n";
					}

					await ReplyAsync(message);
				}

				[Command("connections")]
				public async Task Connections()
				{
					Player player = GetPlayer();

					string message = "";

					foreach (Connection connection in ChatCraft.Instance.State.connections)
					{
						message += $"{connection.locationA.name} {connection.locationB.name} {connection.chanceToFindA} {connection.chanceToFindB}\n";
					}

					await ReplyAsync(message);
				}

				[Command("players")]
				public async Task Players()
				{
					Player userPlayer = GetPlayer();

					string message = "";

					IReadOnlyCollection<SocketGuildUser> users = Context.Guild.Users;
					
					foreach (Player player in ChatCraft.Instance.State.players)
					{
						IGuildUser user = users.FirstOrDefault(test => test.Id == player.identifier);

						if (user != null)
						{
							message += $"'{user.Username}'\n";
						}
					}

					await ReplyAsync(message);
				}
			}

			[Group("a")]
			public class Adding : CrierModuleBase
			{
				[Command("Slot")]
				public async Task Slot(string name, int side)
				{
					Player player = GetPlayer();

					Slot newSlot = new Slot()
					{
						names = new List<string>() { name },
						side = side,
						allowedTypes = new List<ItemType>()
					};

					ChatCraft.Instance.State.slots.Add(newSlot);

					await ReplyAsync("Slot added: " + name);
				}

				[Command("Stat")]
				public async Task Stat(string name)
				{
					Player player = GetPlayer();

					Stat newStat = new Stat()
					{
						name = name
					};

					ChatCraft.Instance.State.stats.Add(newStat);

					await ReplyAsync("Stat added: " + name);
				}

				[Command("Item")]
				public async Task Item(string name, string description, ItemType itemType)
				{
					Player player = GetPlayer();

					Item newItem = new Item()
					{
						name = name,
						description = description,
						itemType = itemType
					};

					if (itemType == ItemType.Armor || itemType == ItemType.Tool)
					{
						newItem.durability = 50;
					}

					ChatCraft.Instance.State.items.Add(newItem);

					await ReplyAsync("Item added: " + name);
				}

				[Command("ItemStat")]
				public async Task ItemStat(string itemName, string statName, int value)
				{
					Player player = GetPlayer();

					Item item = ChatCraft.Instance.State.items.FirstOrDefault(test => test.name.ToLower() == itemName.ToLower());

					if (item == null)
					{
						await ReplyAsync("There is no item called " + itemName);
						return;
					}

					Stat stat = ChatCraft.Instance.State.stats.FirstOrDefault(test => test.name == statName);

					if (stat == null)
					{
						await ReplyAsync("There is no stat called " + statName);
						return;
					}

					StatCount count = item.statModifications.FirstOrDefault(test => test.stat == stat);

					if (count == null)
					{
						count = new StatCount()
						{
							stat = stat
						};

						item.statModifications.Add(count);
					}

					count.count = value;

					await ReplyAsync("Item modified: " + itemName);
				}

				[Command("Location")]
				public async Task Location(string name)
				{
					Player player = GetPlayer();

					Location newLocation = new Location()
					{
						name = name
					};

					ChatCraft.Instance.State.locations.Add(newLocation);

					await ReplyAsync("Location added: " + name);
				}

				[Command("LocationDescription")]
				public async Task LocationDescription(Location location, string description)
				{
					Player player = GetPlayer();

					location.descriptions.Add(description);

					await ReplyAsync("Location modified: " + location.name);
				}

				[Command("Connection")]
				public async Task Connection(Location location1, Location location2, float location1FindChance, float location2FindChance)
				{
					Player player = GetPlayer();

					Connection connection = new Connection()
					{
						locationA = location1,
						locationB = location2,
						chanceToFindA = location1FindChance,
						chanceToFindB = location2FindChance
					};

					location1.connections.Add(connection);
					location2.connections.Add(connection);

					ChatCraft.Instance.State.connections.Add(connection);

					await ReplyAsync("Connection added");
				}

				[Command("Recipe")]
				public async Task Recipe(string name, string description)
				{
					Player player = GetPlayer();

					Recipe newRecipe = new Recipe()
					{
						name = name,
						description = description
					};

					ChatCraft.Instance.State.recipes.Add(newRecipe);

					await ReplyAsync("Recipe added: " + name);
				}

				[Command("RecipeChance")]
				public async Task RecipeCost(Recipe recipe, float chance)
				{
					Player player = GetPlayer();

					recipe.successChance = chance;

					await ReplyAsync("Recipe modified: " + recipe.name);
				}

				[Command("RecipeCost")]
				public async Task RecipeCost(Recipe recipe, Item item, int count)
				{
					Player player = GetPlayer();

					recipe.consumed.Add(new ItemCount(item, count));

					await ReplyAsync("Recipe modified: " + recipe.name);
				}

				[Command("RecipeResult")]
				public async Task RecipeResult(Recipe recipe, Item item, int count)
				{
					Player player = GetPlayer();

					recipe.creates.Add(new ItemCount(item, count));

					await ReplyAsync("Recipe modified: " + recipe.name);
				}

				[Command("RecipeLocation")]
				public async Task RecipeLocation(Recipe recipe, Location location)
				{
					Player player = GetPlayer();

					if (recipe.allowedLocations.Contains(location))
					{
						recipe.allowedLocations.Remove(location);
						await ReplyAsync(recipe.name + " no longer expects : " + location.name);
						return;
					}

					recipe.allowedLocations.Add(location);

					await ReplyAsync(recipe.name + " now expects: " + location.name);
				}

				[Command("RecipeStat")]
				public async Task RecipeStat(Recipe recipe, Stat stat, int count)
				{
					Player player = GetPlayer();

					StatCount found = recipe.requiredStatCount.FirstOrDefault(test => test.stat == stat);

					if (found != null)
					{
						if (count == 0)
						{
							recipe.requiredStatCount.Remove(found);
							await ReplyAsync(recipe.name + " no longer expects : " + stat.name);
							return;
						}

						found.count = count;
					}
					else
					{
						recipe.requiredStatCount.Add(new StatCount() { stat = stat, count = count });
					}

					await ReplyAsync(recipe.name + " now expects: " + stat.name + " >= " + count);
				}
			}

			[Command("RecipeItem")]
			public async Task RecipeItem(Recipe recipe, Item item)
			{
				Player player = GetPlayer();

				if (recipe.requiredTools.Contains(item))
				{
					recipe.requiredTools.Remove(item);
					await ReplyAsync(recipe.name + " no longer expects : " + item.name);
					return;
				}

				recipe.requiredTools.Add(item);

				await ReplyAsync(recipe.name + " now expects: " + item.name);
			}

            [Command("checkin")]
            public async Task Checkin(string comment)
            {
                Player player = GetPlayer();

                await ReplyAsync("Starting checkin");

                var process = new Process();
                var startinfo = new ProcessStartInfo("cmd.exe", $@"/C {Directory.GetCurrentDirectory()}/../../../../checkin.bat {comment}");
                startinfo.RedirectStandardOutput = true;
                startinfo.UseShellExecute = false;
                process.StartInfo = startinfo;
                process.OutputDataReceived += async (sender, args) =>
                {
                    if (args.Data != null && args.Data.Length > 0)
                    {
                        await Context.Channel.SendMessageAsync(args.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }

            [Command("learn all recipes")]
            public async Task LearnRecipes()
            {
                Player player = GetPlayer();

                player.recipes.Clear();
                player.recipes.AddRange(ChatCraft.Instance.State.recipes);

                await ReplyAsync("With power comes great responsibility.");
            }

            [Command("clear inventory")]
            public async Task ClearInventory()
            {
                Player player = GetPlayer();

                player.items.Clear();

                await ReplyAsync("Well aren't you an unlucky bugger.");
            }

            [Command("fill inventory")]
            public async Task GiveAllItems()
            {
                Player player = GetPlayer();

                foreach (Item item in ChatCraft.Instance.State.items)
                {
                    if (item.itemType == ItemType.Armor || item.itemType == ItemType.Tool)
                    {
                        player.items.Add(new ItemCount(item, item.durability));
                    }
                    else
                    {
                        ItemCount existing = player.items.FirstOrDefault(count => count.item == item);

                        if (existing != null)
                        {
                            existing.count++;
                        }
                        else
                        {
                            player.items.Add(new ItemCount(item, 10));
                        }
                    }
                }

                await ReplyAsync("Well aren't you a lucky bugger.");
            }
        }

	}
}
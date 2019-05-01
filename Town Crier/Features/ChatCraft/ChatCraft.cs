using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace DiscordBot.Modules.ChatCraft
{
	public class ChatCraft
	{
		public static Random Random { get; } = new Random();

		public static ChatCraft Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new ChatCraft();
				}

				return instance;
			}
		}

		public static void Initialize()
		{
			if (instance == null)
			{
				instance = new ChatCraft();
			}
		}

		static ChatCraft instance;

		public ChatCraftState State { get { return state; } }

		ChatCraftState state;

		Timer timer = new Timer(5f * 60f * 1000f);

		Dictionary<ulong, Player> playerMap = new Dictionary<ulong, Player>();

		ChatCraft()
		{
			Load();

			timer.Elapsed += (sender, e) => Save();
			timer.Start();

			Save();
		}
		
		~ChatCraft()
		{
			Save();

			timer.Stop();
		}

		void Load()
		{
			state = FileDatabase.Read<ChatCraftState>("ChatCraft/craftConfig", new SlotDictionaryConverter());

			if (state == null)
			{
				state = new ChatCraftState();
				state.stats.Add(new Stat());
				state.settings.defaultStats.Add(new StatCount() { stat = state.stats[0] });
				state.items.Add(new Item());
				state.items[0].craftForwardChances.Add(new ItemWeight());
				state.items[0].statModifications.Add(new StatCount());
				state.recipes.Add(new Recipe());
				state.recipes[0].consumed.Add(new ItemCount());
				state.recipes[0].creates.Add(new ItemCount());
				state.itemSets.Add(new ItemSet());
				state.locations.Add(new Location());
				state.connections.Add(new Connection());
				state.encounters.Add(new Encounter());
				state.encounters[0].possibleEnemies.Add(new EnemyDefinition());
				state.encounters[0].possibleLoot.Add(new ItemWeightCount());
				state.players.Add(new Player() { identifier = 0, currentLocation = state.locations[0] } );
				state.players[0].items.Add(new ItemCount());
				state.players[0].locations.Add(new Location());
				state.players[0].recipes.Add(new Recipe());
			}

			if (state.combatInstances == null)
			{
				state.combatInstances = new List<CombatInstance>();
			}

			foreach (Connection connection in state.connections)
			{
				if (connection.locationA == null || connection.locationB == null)
				{
					Console.WriteLine("Missing location on connection.");
					continue;
				}

				connection.locationA.connections.Add(connection);
				connection.locationB.connections.Add(connection);
			}

			foreach (Party party in state.parties)
			{
				foreach (Unit unit in party.currentUnits)
				{
					unit.party = party;
				}
			}

			state.combatInstances.RemoveAll(item => item == null);
		
			foreach (CombatInstance instance in state.combatInstances)
			{
				for (int i = 0; i < 2; i++)
				{
					if (instance == null || instance.teams[i] == null || instance.teams[i].currentUnits == null)
					{
						Console.WriteLine(instance == null);

						Console.WriteLine(instance?.teams[i] == null);

						Console.WriteLine(instance?.teams[i]?.currentUnits == null);

						continue;
					}

					foreach (Unit unit in instance.teams[i].currentUnits)
					{
						if (unit.combatState == null)
						{
							unit.combatState = new CombatState();
						}

						unit.combatState.instance = instance;
						unit.combatState.teamIndex = i;
					}
				}
			}

			foreach (Player player in state.players)
			{
				Slot[] slots = player.equipped.Keys.ToArray();

				foreach (Slot slot in slots)
				{
					if (!state.slots.Contains(slot))
					{
						player.equipped.Remove(slot);
					}
				}

				foreach (Slot slot in state.slots)
				{
					if (!player.equipped.ContainsKey(slot))
					{
						player.equipped.Add(slot, null);
					}
				}
			}

			foreach (Item item in state.items)
			{
				for (int i = 0; i < item.statModifications.Count; i++)
				{
					if (item.statModifications[i].stat == null)
					{
						item.statModifications.RemoveAt(i);
						i--;
					}
				}
			}

			foreach (Player player in state.players)
			{
				playerMap.Add(player.identifier, player);
			}

			Console.WriteLine("Craft State Loaded");
		}
		
		public void Save()
		{
            state.savesSinceBackup = (state.savesSinceBackup + 1) % 500;

			state.combatInstances.Clear();

			foreach (Player player in state.players)
			{
				if (player.combatState != null && !state.combatInstances.Contains(player.combatState.instance))
				{
					state.combatInstances.Add(player.combatState.instance);
				}
			}

			FileDatabase.Write("ChatCraft/craftConfig", state, state.savesSinceBackup == 0, new SlotDictionaryConverter());
		}

		public async Task ApplyFixes(IGuild guild)
		{
			foreach (Player player in state.players)
			{
				try
				{
					if (player.joined == default(DateTime))
					{
						IGuildUser user = await guild.GetUserAsync(player.identifier);
						player.joined = user.JoinedAt.Value.UtcDateTime;
					}
				}
				catch (Exception e)
				{
					Console.WriteLine("Failed for " + player.name + " " + e.Message);
				}
			}
		}

		public Player GetExistingPlayer(IUser user)
		{
			Player found;

			if (!playerMap.TryGetValue(user.Id, out found))
			{
				return null;
			}

			return found;
		}

		public Player GetPlayer(IUser user)
		{
			Player found;
			
			if (!playerMap.TryGetValue(user.Id, out found))
			{
				Player copyFrom = state.players[0];

				found = new Player()
				{
					identifier = user.Id,
					
					coins = copyFrom.coins,

					currentLocation = copyFrom.currentLocation,

					items = new List<ItemCount>(copyFrom.items),
					locations = new List<Location>(copyFrom.locations),
					recipes  = new List<Recipe>(copyFrom.recipes),
				};

				try
				{
					IGuildUser guildUser = user as IGuildUser;

					if (guildUser != null)
					{
						found.joined = guildUser.JoinedAt.Value.UtcDateTime;
					}
				}
				catch (Exception e)
				{
					Console.WriteLine("Failed to set joined date. ");
					Console.WriteLine(e.Message);
				}


				foreach (KeyValuePair<Slot, ItemCount> equipped in copyFrom.equipped)
				{
					found.equipped.Add(equipped.Key, equipped.Value == null ? null : new ItemCount(equipped.Value.item, equipped.Value.count));
				}
				
				state.players.Add(found);

				playerMap.Add(found.identifier, found);

                foreach (Slot slot in state.slots)
                {
                    if (!found.equipped.ContainsKey(slot))
                    {
                        found.equipped.Add(slot, null);
                    }
                }

				Save();
			}

			found.name = user.Username;

			return found;
		}
	}
}

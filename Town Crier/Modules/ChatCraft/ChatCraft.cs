using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

	public enum ItemType
	{
		General = 0,

		Consumable = 1,

		Tool = 10,
        StackingTool = 11,

		Armor = 12,

		Pendant = 20,
		Ring = 21
	}
	
	public class ChatCraftState
	{
		public int savesSinceBackup;
		public List<Stat> stats = new List<Stat>();
		public List<Slot> slots = new List<Slot>();
		public List<Item> items = new List<Item>();
		public List<Recipe> recipes = new List<Recipe>();
		public List<Encounter> encounters = new List<Encounter>();
		public List<ItemSet> itemSets = new List<ItemSet>();
		public List<RecipeSet> recipeSets = new List<RecipeSet>();
		public List<EncounterSet> encounterSets = new List<EncounterSet>();
		public List<ExploreSet> exploreSets = new List<ExploreSet>();
		public List<Location> locations = new List<Location>();
		public List<Connection> connections = new List<Connection>();
		public List<Player> players = new List<Player>();
		public List<Party> parties = new List<Party>();
		public List<CombatInstance> combatInstances = new List<CombatInstance>();
		public Settings settings = new Settings();
	}

	public class Settings
	{
		public List<StatCount> defaultStats = new List<StatCount>();
	}

	public class Slot
	{
		public List<string> names = new List<string>();
		public List<ItemType> allowedTypes = new List<ItemType>();
		public int side = 0;
	}

	public class Stat
	{
		public string name;
        public bool isTool;
		public bool isHandDependant;
        public string emoji;
	}

	public class StatCount
	{
		public Stat stat;
		public int count;
	}

	public class StatWeight
	{
		public Stat stat;
		public float weight;
	}

	public class Item
	{
		public string name;
		public string emoji;
		public ItemType itemType;
		public string description;
		public List<ItemWeight> craftForwardChances = new List<ItemWeight>();
		public List<StatCount> statModifications = new List<StatCount>();
		public int durability;

		public static Item Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.items.FirstOrDefault(item => item.name.ToLower() == name);
		}
	}

	public class ItemCount
	{
		public Item item;
		public int count;

		public ItemCount() { }

		public ItemCount(Item item, int count)
		{
			this.item = item;
			this.count = count;
		}
    }

	public class ItemWeight
	{
		public Item item;
		public float weight;
	}

	public class ItemWeightCount : ItemWeight
	{
		public int minimum = 1;
		public int maximum = 1;
	}

	public class Recipe
	{
		public string name;
        public string description;
        public List<Location> allowedLocations = new List<Location>();
        public List<Item> requiredTools = new List<Item>();
		public List<StatCount> requiredStatCount = new List<StatCount>();
		public List<ItemCount> consumed = new List<ItemCount>();
		public List<ItemCount> creates = new List<ItemCount>();
        public float successChance = 1f;

		public static Recipe Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.recipes.FirstOrDefault(item => item.name.ToLower() == name);
		}
	}

	public class RecipeCount
	{
		public Recipe recipe;
		public int count;
	}

	public class RecipeWeight
	{
		public Recipe recipe;
		public float weight;
	}

	public class ItemSet
	{
		public string name;
		public List<ItemWeightCount> itemWeights = new List<ItemWeightCount>();
		public List<ItemSet> subSets = new List<ItemSet>();

		public static ItemSet Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.itemSets.FirstOrDefault(item => item.name.ToLower() == name);
		}

		public void GetBest(Random random, ref ItemWeightCount best, ref double bestWeight)
		{
			foreach (ItemWeightCount item in itemWeights)
			{
				double value = item.weight * random.NextDouble();

				if (value < bestWeight)
				{
					bestWeight = value;
					best = item;
				}
			}

			foreach (ItemSet set in subSets)
			{
				set.GetBest(random, ref best, ref bestWeight);
			}
		}
	}

	public class RecipeSet
	{
		public string name;
		public List<RecipeWeight> recipeWeights = new List<RecipeWeight>();
		public List<RecipeSet> subSets = new List<RecipeSet>();

		public static RecipeSet Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.recipeSets.FirstOrDefault(item => item.name.ToLower() == name);
		}

		public void GetBest(Random random, ref Recipe best, ref double bestWeight)
		{
			foreach (RecipeWeight item in recipeWeights)
			{
				double value = item.weight * random.NextDouble();

				if (value < bestWeight)
				{
					bestWeight = value;
					best = item.recipe;
				}
			}

			foreach (RecipeSet set in subSets)
			{
				set.GetBest(random, ref best, ref bestWeight);
			}
		}
	}

	public class EncounterSet
	{
		public string name;
		public List<EncounterWeight> encounterWeights = new List<EncounterWeight>();
		public List<EncounterSet> subSets = new List<EncounterSet>();

		public static EncounterSet Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.encounterSets.FirstOrDefault(item => item.name.ToLower() == name);
		}
		
		public void GetBest(Random random, ref Encounter best, ref double bestWeight)
		{
			foreach (EncounterWeight item in encounterWeights)
			{
				double value = item.weight * random.NextDouble();

				if (value < bestWeight)
				{
					bestWeight = value;
					best = item.encounter;
				}
			}

			foreach (EncounterSet set in subSets)
			{
				set.GetBest(random, ref best, ref bestWeight);
			}
		}
	}

	public class ExploreSet
	{
		public string name;
		public ItemSet itemSet = new ItemSet();
		public RecipeSet recipeSet = new RecipeSet();
		public EncounterSet encounterSet = new EncounterSet();

		public static ExploreSet Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.exploreSets.FirstOrDefault(item => item.name.ToLower() == name);
		}
	}

	public class Location
	{
		public string name;
        public List<string> descriptions = new List<string>();
		public ExploreSet exploreSet;

		[JsonIgnore]
		public List<Connection> connections = new List<Connection>();

		public static Location Find(string name)
		{
			name = name.Trim().ToLower();
			return ChatCraft.Instance.State.locations.FirstOrDefault(item => item.name.ToLower() == name);
		}
	}

	public class Connection
	{
		public Location locationA;
		public Location locationB;
		public float chanceToFindA;
		public float chanceToFindB;
	}

	public abstract class Unit
	{
		public class StatGroup
		{
			public int Total { get { return shared + left + right; } }
			public int LeftTotal { get { return shared + left; } }
			public int RightTotal { get { return shared + right; } }

			public Unit unit;
			public Stat stat;
			public int shared;
			public int left;
			public int right;
			public int baseValue;
			public int modified;

			public void Modify(int add)
			{
				StatCount modified = unit.modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

				if (modified == null)
				{
					modified = new StatCount()
					{
						stat = stat,
						count = this.modified
					};

					unit.modifiedBaseStats.Add(modified);
				}

				modified.count += add;
				this.modified = modified.count;

				Update();
			}

			public void SetModified(int setTo)
			{
				StatCount modified = unit.modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

				if (modified == null)
				{
					modified = new StatCount()
					{
						stat = stat,
						count = setTo
					};

					unit.modifiedBaseStats.Add(modified);

					return;
				}

				modified.count = setTo;
				this.modified = modified.count;

				Update();
			}

			public void Update()
			{
				shared = modified;
				right = 0;
				left = 0;

				foreach (KeyValuePair<Slot, ItemCount> equipment in unit.equipped)
				{
					if (equipment.Value != null)
					{
						StatCount modification = equipment.Value.item.statModifications.FirstOrDefault(test => test.stat == stat);

						if (modification != null)
						{
							if (equipment.Key.side == 0 || !stat.isHandDependant)
							{
								shared += modification.count;
							}
							else if (equipment.Key.side == 1)
							{
								right = modification.count;
							}
							else
							{
								left = modification.count;
							}
						}
					}
				}
			}
		}

		[JsonIgnore]
		public abstract bool IsReady { get; }

		public string name;

		public List<ItemCount> items = new List<ItemCount>();

		public Dictionary<Slot, ItemCount> equipped = new Dictionary<Slot, ItemCount>();

		public List<StatCount> modifiedBaseStats = new List<StatCount>();

		public CombatState combatState;

		[JsonIgnore]
		public Party party;
		
		public StatGroup GetStat(string name)
		{
			Stat stat = ChatCraft.Instance.State.stats.FirstOrDefault(test => test.name == name);

			if (stat == null)
			{
				return null;
			}

			return GetStat(stat);
		}

		public StatGroup GetStat(Stat stat)
		{
			StatGroup result = new StatGroup();

			result.unit = this;
			result.stat = stat;

			StatCount defaultValue = ChatCraft.Instance.State.settings.defaultStats.FirstOrDefault(test => test.stat == stat);

			if (defaultValue != null)
			{
				result.baseValue = result.modified = defaultValue.count;
			}

			StatCount modified = modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

			if (modified != null)
			{
				result.modified = modified.count;
			}

			result.Update();

			return result;
		}

		public Dictionary<Stat, StatGroup> GetStats()
		{
			Dictionary<Stat, StatGroup> stats = new Dictionary<Stat, StatGroup>();

			foreach (Stat stat in ChatCraft.Instance.State.stats)
			{
				stats.Add(stat, new StatGroup());
			}

			foreach (StatCount stat in ChatCraft.Instance.State.settings.defaultStats)
			{
				stats[stat.stat].shared = stat.count;
			}

            foreach (StatCount stat in modifiedBaseStats)
            {
                stats[stat.stat].shared = stat.count;
            }

            foreach (KeyValuePair<Slot, ItemCount> equipment in equipped)
			{
				if (equipment.Value != null)
				{
					foreach (StatCount stat in equipment.Value.item.statModifications)
					{
						if (equipment.Key.side == 0 || !stat.stat.isHandDependant)
						{
							stats[stat.stat].shared += stat.count;
						}
						else if (equipment.Key.side == 1)
						{
							stats[stat.stat].right = stat.count;
						}
						else
						{
							stats[stat.stat].left = stat.count;
						}
					}
				}
			}
			
			return stats;
		}
		
		public ItemCount TakeEquipment(ItemCount itemCount)
		{
			switch (itemCount.item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				itemCount.count--;
				
				if (itemCount.count == 0)
				{
					items.Remove(itemCount);
				}

				return new ItemCount(itemCount.item, 1);

				case ItemType.Tool:
				case ItemType.Armor:

				items.Remove(itemCount);
				return itemCount;
			}

			return null;
		}

		public void ReturnEquipment(ItemCount equipment)
		{
			switch (equipment.item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				ItemCount itemCount = items.FirstOrDefault(test => test.item == equipment.item);

				if (itemCount == null)
				{
					itemCount = new ItemCount(equipment.item, 0);
					items.Add(itemCount);
				}

				itemCount.count += equipment.count;
				break;

				case ItemType.Tool:
				case ItemType.Armor:

				items.Add(equipment);
				break;
			}
		}

		public void AddItem(Item item, int count = 1)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:
				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount == null)
				{
					itemCount = new ItemCount(item, 0);
					items.Add(itemCount);
				}

				itemCount.count += count;
				break;

				case ItemType.Tool:
				case ItemType.Armor:
				for (int i = 0; i < count; i++)
				{
					itemCount = new ItemCount(item, item.durability);
					items.Add(itemCount);
				}
				break;
			}
		}

		public int ItemCount(Item item)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount == null)
				{
					return 0;
				}

				return itemCount.count;

				case ItemType.Tool:
				case ItemType.Armor:

				return items.Count(test => test.item == item);
			}

			return 0;
		}

		public void ConsumeItem(Item item, int count = 1)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount != null)
				{
					itemCount.count = Math.Max(0, itemCount.count - count);

					if (itemCount.count == 0)
					{
						items.Remove(itemCount);
					}
				}

				break;

				case ItemType.Tool:
				case ItemType.Armor:
				for (int i = 0; i < count; i++)
				{
					itemCount = items.FirstOrDefault(test => test.item == item);

					if (itemCount != null)
					{
						items.Remove(itemCount);
					}
				}
				break;
			}
		}

		public float GetLuck()
		{
			Stat luckStat = ChatCraft.Instance.State.stats.FirstOrDefault(test => test.name == "Luck");

			if (luckStat == null)
			{
				Console.WriteLine("Luck Stat not found");
			}

			int luck = luckStat == null ? 20 : GetStats()[luckStat].Total;

			return luck / 20f;
		}
	}

	public class Player : Unit
	{
		[JsonIgnore]
		public override bool IsReady { get { return isReady; } }

		[JsonIgnore]
		public bool isReady;

		public int bet;

		public ulong identifier;
		
		public int coins;

		public Location currentLocation;
		
		public List<Location> locations = new List<Location>();
		public List<Recipe> recipes = new List<Recipe>();

		public bool isAdmin;

		public int sparWins;
		public int spars;

		public DateTime joined;

		public DateTime lastMessage;
		public uint score;

		public uint usedHourPoints = 0;
		public DateTime usedFirstHourPoint;
	}

	public class Encounter
	{
		public string name;
		public List<string> possibleDescriptions = new List<string>();
		public List<EnemyDefinition> possibleEnemies = new List<EnemyDefinition>();
		public int minimumEnemies = 1;
		public int maximumEnemies = 3;
		public float enemyLootMultiplier;
		public List<ItemWeightCount> possibleLoot = new List<ItemWeightCount>();
		//public //Chests? Definition/???
	}

	public class EnemyDefinition
	{
		public string name;
		public List<string> possibleDescriptions = new List<string>();
		public List<StatCount> stats = new List<StatCount>();
		public List<ItemWeight> toolPossibilities = new List<ItemWeight>();
		public List<ItemWeight> armorPossibiities = new List<ItemWeight>();
		public List<ItemWeight> pendantPossibilities = new List<ItemWeight>();
		public List<ItemWeight> ringPossibilities = new List<ItemWeight>();
		public List<ItemWeight> possibleLoot = new List<ItemWeight>();
	}

	public class EncounterCount
	{
		public Encounter encounter;
		public int count;
	}

	public class EncounterWeight
	{
		public Encounter encounter;
		public float weight;
	}

	class SlotDictionaryConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(Dictionary<Slot, ItemCount>).IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			Dictionary<Slot, ItemCount> dictionary = new Dictionary<Slot, ItemCount>();

			JArray array = JToken.Load(reader) as JArray;
			
			if (array == null)
			{
				return dictionary;
			}

			foreach (JObject jObject in array)
			{
				JToken result;

				if (jObject.TryGetValue("Key", out result))
				{
					Slot slot = result.ToObject<Slot>(serializer);
					
					if (slot != null)
					{
						dictionary.Add(slot, jObject.GetValue("Value").ToObject<ItemCount>(serializer));
					}
				}
			}

			return dictionary;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			IDictionary<Slot, ItemCount> dictionary = (IDictionary<Slot, ItemCount>)value;

			JArray array = new JArray();

			foreach (var kvp in dictionary)
			{
				JObject child = new JObject();
				child.Add("Key", JToken.FromObject(kvp.Key, serializer));
				child.Add("Value", kvp.Value == null ? null : JToken.FromObject(kvp.Value, serializer));

				array.Add(child);
			}

			array.WriteTo(writer);
		}
	}
}

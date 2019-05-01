using System.Collections.Generic;

namespace DiscordBot.Modules.ChatCraft
{
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
}

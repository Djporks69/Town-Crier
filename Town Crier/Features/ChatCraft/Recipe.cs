using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

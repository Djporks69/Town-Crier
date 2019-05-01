using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

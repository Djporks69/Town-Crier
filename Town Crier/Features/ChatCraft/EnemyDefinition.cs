using System.Collections.Generic;

namespace DiscordBot.Modules.ChatCraft
{
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
}

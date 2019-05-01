using System.Collections.Generic;

namespace DiscordBot.Modules.ChatCraft
{
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
}

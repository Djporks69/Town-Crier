using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

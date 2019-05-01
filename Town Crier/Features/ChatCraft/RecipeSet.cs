using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

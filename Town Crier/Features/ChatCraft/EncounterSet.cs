using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

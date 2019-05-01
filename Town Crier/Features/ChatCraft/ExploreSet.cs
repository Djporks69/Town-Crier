using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

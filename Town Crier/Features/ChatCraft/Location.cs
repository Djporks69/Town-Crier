using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
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
}

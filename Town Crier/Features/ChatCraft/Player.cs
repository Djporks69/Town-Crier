using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DiscordBot.Modules.ChatCraft
{
	public class Player : Unit
	{
		[JsonIgnore]
		public override bool IsReady { get { return isReady; } }

		[JsonIgnore]
		public bool isReady;

		public int bet;

		public ulong identifier;
		
		public int coins;

		public Location currentLocation;
		
		public List<Location> locations = new List<Location>();
		public List<Recipe> recipes = new List<Recipe>();

		public bool isAdmin;

		public int sparWins;
		public int spars;

		public DateTime joined;

		public DateTime lastMessage;
		public uint score;

		public uint usedHourPoints = 0;
		public DateTime usedFirstHourPoint;
	}
}

using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.ChatCraft
{
	public class CombatState
	{
		[JsonIgnore]
		public CombatInstance instance;

		public int teamIndex;
		public bool isLeftHandAttacked;
		public bool isRightHandAttacked;
	}

	public class NPC : Unit
	{
		public override bool IsReady { get { return true; } }
	}

	public class Party
	{
		public const int MaximumUnits = 10;

		public IEnumerable<Unit> AllUnits => currentUnits.Concat(fledUnits.Concat(deadUnits));

		public string name;
		public List<Unit> currentUnits = new List<Unit>();
		public List<Unit> fledUnits = new List<Unit>();
		public List<Unit> deadUnits = new List<Unit>();
		public List<Unit> invited = new List<Unit>();
		public Location location;
		
		public string SafeName(bool isStartOfSentence = false)
		{
			if (name != null)
			{
				return $"'{name}'";
			}

			return isStartOfSentence ? "The party" : "the party";
		}

		public void Add(Unit unit)
		{
			unit.party = this;
			currentUnits.Add(unit);
		}

		public void Flee(Unit unit)
		{
			currentUnits.Remove(unit);
			fledUnits.Add(unit);
		}

		public void Killed(Unit unit)
		{
			currentUnits.Remove(unit);
			deadUnits.Add(unit);
		}

		public override string ToString()
		{
			string result = name == null ? "" : ("***" + name + "***\n");

			AddList("Alive", currentUnits, ref result);
			AddList("Killed", deadUnits, ref result);
			AddList("Fled", fledUnits, ref result);

			return result;
		}
		
		public string InviteList()
		{
			string result = "";

			AddList("Invited", invited, ref result);

			return result;
		}
		
		void AddList(string name, List<Unit> list, ref string text)
		{
			if (list.Count > 0)
			{
				text += $"**{name}**\n";
			}

			foreach (Unit unit in list)
			{
				text += unit.name + "\n";
			}
		}
	}

	public class CombatInstance
	{
		public bool isOver;

		public bool isSpar;

		public List<Unit> allUnits = new List<Unit>();

		public int round = 0;

		public Party[] teams = new Party[]
		{
			new Party(), new Party()
		};

		public bool Add(Unit unit, int team)
		{
			if (allUnits.Contains(unit))
			{
				return false;
			}

			allUnits.Add(unit);

			if (!isOver && unit.combatState == null)
			{
				unit.combatState = new CombatState()
				{
					teamIndex = team,
					instance = this
				};

				teams[team].Add(unit);
			}

			return true;
		}
		
		public void Flee(Unit unit)
		{
			if (!isOver && unit.combatState != null && unit.combatState.instance == this)
			{
				Party team = teams[unit.combatState.teamIndex];
				
				team.Flee(unit);
				unit.combatState = null;

				if (team.currentUnits.Count == 0)
				{
					End();
				}
			}
		}

		public void Killed(Unit unit)
		{
			if (!isOver && unit.combatState != null && unit.combatState.instance == this)
			{
				Party team = teams[unit.combatState.teamIndex];

				team.Killed(unit);
				unit.combatState = null;

				if (team.currentUnits.Count == 0)
				{
					End();
				}
			}
		}

		public void End()
		{
			if (isOver)
			{
				return;
			}

			foreach (Party team in teams)
			{
				foreach (Unit unit in team.currentUnits)
				{
					unit.combatState = null;

					if (isSpar)
					{
						Player player = unit as Player;

						if (player != null)
						{
							player.sparWins++;
						}
					}
				}
			}

			isOver = true;
		}
	}


	[Group("town"), Alias("tc")]
	public class TownCommandsCombat : CrierModuleBase
	{
		[Group(), Alias("combat")]
		public class CombatCommandsOptional : CrierModuleBase
		{
			[Group("attack")]
			public class AttackCommands : CrierModuleBase
			{
				[Command()]
				public async Task AttackUser([Hand]Slot hand, [InCombatWith, Enemy]Unit target)
				{
					Player player = GetPlayer();

					bool isAttacked;
					bool isLeft = hand.names.Contains("lh");

					if (isLeft)
					{
						isAttacked = player.combatState.isLeftHandAttacked;
						player.combatState.isLeftHandAttacked = true;
					}
					else
					{
						isAttacked = player.combatState.isRightHandAttacked;
						player.combatState.isRightHandAttacked = true;
					}

					if (isAttacked)
					{
						await ReplyAsync("You have already attacked with that hand.");
						return;
					}

					ItemCount held = player.equipped[hand];

					int damage = 1;

					if (held == null)
					{
						await ReplyAsync($"{player.name} punched {target.name} dealing {damage} damage.");
					}
					else
					{
						Item item = held.item;

						Unit.StatGroup damageStat = player.GetStat("Damage");
						damage = isLeft ? damageStat.LeftTotal : damageStat.RightTotal;

						await ReplyAsync($"{player.name} attacked {target.name} with {item.name} dealing {damage} damage.");
					}

					Unit.StatGroup healthStat = target.GetStat("Health");
					healthStat.Modify(-damage);

					if (healthStat.Total > 0)
					{
						await ReplyAsync($"{target.name} is on {healthStat.Total} health.");
					}
					else
					{
						await ReplyAsync($"{target.name} was killed.");

						CombatInstance combat = target.combatState.instance;

						combat.Killed(target);
						
						if (combat.isOver)
						{
							await ReplyAsync("The fight is over.");
						}
					}
				}
			}


			[Command("spar")]
			public async Task Spar(IUser opponent)
			{
				Player player = GetPlayer();
				Player other = GetPlayer(opponent);

				if (player.combatState != null)
				{
					await ReplyAsync("You are already in combat!");
					return;
				}

				if (other.combatState != null)
				{
					await ReplyAsync($"{opponent.Username} is already in combat!");
					return;
				}

				CombatInstance combat = new CombatInstance();
				combat.Add(player, 0);
				combat.Add(other, 1);

				player.spars++;
				other.spars++;

				await ReplyAsync($"{player.name} and {other.name} are now in combat!");
			}


			[Command("flee")]
			public async Task Flee()
			{
				Player player = GetPlayer();

				if (player.combatState == null)
				{
					await ReplyAsync("You aren't in combat!");
					return;
				}

				CombatInstance combat = player.combatState.instance;

				combat.Flee(player);

				await ReplyAsync("You fled the fight!");

				if (combat.isOver)
				{
					await ReplyAsync("The fight is over.");
				}
			}
		}

		[Group("combat")]
		public class CombatCommands : CrierModuleBase
		{
			[Command("state"), Priority(1)]
			public async Task State()
			{
				Player player = GetPlayer();

				if (player.combatState == null)
				{
					await ReplyAsync("You are not in combat.");
					return;
				}

				CombatInstance combat = player.combatState.instance;

				string attackState = null;

				if (player.combatState.isLeftHandAttacked && player.combatState.isRightHandAttacked)
				{
					attackState = "You have already attacked.";
				}
				else if (!player.combatState.isLeftHandAttacked && !player.combatState.isRightHandAttacked)
				{
					attackState = "You have not attacked.";
				}
				else if (player.combatState.isLeftHandAttacked)
				{
					attackState = "You have not attacked with your right hand.";
				}
				else
				{
					attackState = "You have not attacked with your left hand.";
				}

				string result = $"You are in battle!\nRound {combat.round}\n{attackState}\n**Team 1**\n{combat.teams[0]}\n**Team 2**\n{combat.teams[1]}";

				await ReplyAsync(result);
			}

			[Command("stats"), Priority(1)]
			public async Task Stats()
			{
				Console.WriteLine("Stats!");

				Player player = GetPlayer();

				if (player.spars == 0)
				{
					await ReplyAsync("You haven't sparred yet!");
					return;
				}

				string percent = $"*(%{ (100f * player.sparWins / player.spars).ToString("0.00")})*";

				await ReplyAsync($"You have won {player.sparWins} {percent} of your spars.");
			}
		}
	}
}

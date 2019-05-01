using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Modules.ChatCraft
{
	public abstract class Unit
	{
		public class StatGroup
		{
			public int Total { get { return shared + left + right; } }
			public int LeftTotal { get { return shared + left; } }
			public int RightTotal { get { return shared + right; } }

			public Unit unit;
			public Stat stat;
			public int shared;
			public int left;
			public int right;
			public int baseValue;
			public int modified;

			public void Modify(int add)
			{
				StatCount modified = unit.modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

				if (modified == null)
				{
					modified = new StatCount()
					{
						stat = stat,
						count = this.modified
					};

					unit.modifiedBaseStats.Add(modified);
				}

				modified.count += add;
				this.modified = modified.count;

				Update();
			}

			public void SetModified(int setTo)
			{
				StatCount modified = unit.modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

				if (modified == null)
				{
					modified = new StatCount()
					{
						stat = stat,
						count = setTo
					};

					unit.modifiedBaseStats.Add(modified);

					return;
				}

				modified.count = setTo;
				this.modified = modified.count;

				Update();
			}

			public void Update()
			{
				shared = modified;
				right = 0;
				left = 0;

				foreach (KeyValuePair<Slot, ItemCount> equipment in unit.equipped)
				{
					if (equipment.Value != null)
					{
						StatCount modification = equipment.Value.item.statModifications.FirstOrDefault(test => test.stat == stat);

						if (modification != null)
						{
							if (equipment.Key.side == 0 || !stat.isHandDependant)
							{
								shared += modification.count;
							}
							else if (equipment.Key.side == 1)
							{
								right = modification.count;
							}
							else
							{
								left = modification.count;
							}
						}
					}
				}
			}
		}

		[JsonIgnore]
		public abstract bool IsReady { get; }

		public string name;

		public List<ItemCount> items = new List<ItemCount>();

		public Dictionary<Slot, ItemCount> equipped = new Dictionary<Slot, ItemCount>();

		public List<StatCount> modifiedBaseStats = new List<StatCount>();

		public CombatState combatState;

		[JsonIgnore]
		public Party party;
		
		public StatGroup GetStat(string name)
		{
			Stat stat = ChatCraft.Instance.State.stats.FirstOrDefault(test => test.name == name);

			if (stat == null)
			{
				return null;
			}

			return GetStat(stat);
		}

		public StatGroup GetStat(Stat stat)
		{
			StatGroup result = new StatGroup();

			result.unit = this;
			result.stat = stat;

			StatCount defaultValue = ChatCraft.Instance.State.settings.defaultStats.FirstOrDefault(test => test.stat == stat);

			if (defaultValue != null)
			{
				result.baseValue = result.modified = defaultValue.count;
			}

			StatCount modified = modifiedBaseStats.FirstOrDefault(test => test.stat == stat);

			if (modified != null)
			{
				result.modified = modified.count;
			}

			result.Update();

			return result;
		}

		public Dictionary<Stat, StatGroup> GetStats()
		{
			Dictionary<Stat, StatGroup> stats = new Dictionary<Stat, StatGroup>();

			foreach (Stat stat in ChatCraft.Instance.State.stats)
			{
				stats.Add(stat, new StatGroup());
			}

			foreach (StatCount stat in ChatCraft.Instance.State.settings.defaultStats)
			{
				stats[stat.stat].shared = stat.count;
			}

            foreach (StatCount stat in modifiedBaseStats)
            {
                stats[stat.stat].shared = stat.count;
            }

            foreach (KeyValuePair<Slot, ItemCount> equipment in equipped)
			{
				if (equipment.Value != null)
				{
					foreach (StatCount stat in equipment.Value.item.statModifications)
					{
						if (equipment.Key.side == 0 || !stat.stat.isHandDependant)
						{
							stats[stat.stat].shared += stat.count;
						}
						else if (equipment.Key.side == 1)
						{
							stats[stat.stat].right = stat.count;
						}
						else
						{
							stats[stat.stat].left = stat.count;
						}
					}
				}
			}
			
			return stats;
		}
		
		public ItemCount TakeEquipment(ItemCount itemCount)
		{
			switch (itemCount.item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				itemCount.count--;
				
				if (itemCount.count == 0)
				{
					items.Remove(itemCount);
				}

				return new ItemCount(itemCount.item, 1);

				case ItemType.Tool:
				case ItemType.Armor:

				items.Remove(itemCount);
				return itemCount;
			}

			return null;
		}

		public void ReturnEquipment(ItemCount equipment)
		{
			switch (equipment.item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				ItemCount itemCount = items.FirstOrDefault(test => test.item == equipment.item);

				if (itemCount == null)
				{
					itemCount = new ItemCount(equipment.item, 0);
					items.Add(itemCount);
				}

				itemCount.count += equipment.count;
				break;

				case ItemType.Tool:
				case ItemType.Armor:

				items.Add(equipment);
				break;
			}
		}

		public void AddItem(Item item, int count = 1)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:
				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount == null)
				{
					itemCount = new ItemCount(item, 0);
					items.Add(itemCount);
				}

				itemCount.count += count;
				break;

				case ItemType.Tool:
				case ItemType.Armor:
				for (int i = 0; i < count; i++)
				{
					itemCount = new ItemCount(item, item.durability);
					items.Add(itemCount);
				}
				break;
			}
		}

		public int ItemCount(Item item)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				case ItemType.StackingTool:

				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount == null)
				{
					return 0;
				}

				return itemCount.count;

				case ItemType.Tool:
				case ItemType.Armor:

				return items.Count(test => test.item == item);
			}

			return 0;
		}

		public void ConsumeItem(Item item, int count = 1)
		{
			switch (item.itemType)
			{
				case ItemType.General:
				case ItemType.Consumable:
				case ItemType.Pendant:
				case ItemType.Ring:
				ItemCount itemCount = items.FirstOrDefault(test => test.item == item);

				if (itemCount != null)
				{
					itemCount.count = Math.Max(0, itemCount.count - count);

					if (itemCount.count == 0)
					{
						items.Remove(itemCount);
					}
				}

				break;

				case ItemType.Tool:
				case ItemType.Armor:
				for (int i = 0; i < count; i++)
				{
					itemCount = items.FirstOrDefault(test => test.item == item);

					if (itemCount != null)
					{
						items.Remove(itemCount);
					}
				}
				break;
			}
		}

		public float GetLuck()
		{
			Stat luckStat = ChatCraft.Instance.State.stats.FirstOrDefault(test => test.name == "Luck");

			if (luckStat == null)
			{
				Console.WriteLine("Luck Stat not found");
			}

			int luck = luckStat == null ? 20 : GetStats()[luckStat].Total;

			return luck / 20f;
		}
	}
}

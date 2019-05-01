namespace DiscordBot.Modules.ChatCraft
{
	public class ItemCount
	{
		public Item item;
		public int count;

		public ItemCount() { }

		public ItemCount(Item item, int count)
		{
			this.item = item;
			this.count = count;
		}
    }
}

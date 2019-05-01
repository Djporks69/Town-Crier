using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ActivityRoles
{
	public enum ActivityFlag
	{
		Playing = 1 << ActivityType.Playing,
		Streaming = 1 << ActivityType.Streaming,
		Listening = 1 << ActivityType.Listening,
		Watching = 1 << ActivityType.Watching,
	}

	public class ActivityDefinition
	{
		readonly string nameRegex;
		readonly string roleName;
		readonly int activities;

		Dictionary<IGuild, IRole> guildToRoleMap = new Dictionary<IGuild, IRole>();

		public ActivityDefinition(string nameRegex, string roleName, ActivityFlag activities = (ActivityFlag)(-1))
		{
			this.nameRegex = nameRegex;

			this.roleName = roleName;

			this.activities = (int)activities;
		}

		public void InitializeForGuild(IGuild guild)
		{
			if (!guildToRoleMap.ContainsKey(guild))
			{
				IRole role = guild.Roles.FirstOrDefault(test => test.Name == roleName);

				guildToRoleMap.Add(guild, role);
			}
		}

		public async Task ApplyRole(IGuildUser user)
		{
			IRole role;

			if (TryGetRole(user.Guild, out role))
			{
				await ApplyRole(role, user);
			}
		}

		public async Task ApplyRole(SocketGuild guild)
		{
			IRole role;

			if (TryGetRole(guild, out role))
			{
				foreach (IGuildUser user in guild.Users)
				{ 
					await ApplyRole(role, user);
				}
			}
		}

		public async Task RemoveRole(SocketGuild guild)
		{
			IRole role;

			if (TryGetRole(guild, out role))
			{
				foreach (IGuildUser user in guild.Users)
				{
					await user.RemoveRoleAsync(role);
				}
			}
		}
		
		async Task ApplyRole(IRole role, IGuildUser user)
		{
			if (IsMatched(user.Activity))
			{
				if (!user.RoleIds.Contains(role.Id))
				{
					await user.AddRoleAsync(role);
				}
			}
			else if (user.RoleIds.Contains(role.Id))
			{
				await user.RemoveRoleAsync(role);
			}
		}

		bool TryGetRole(IGuild guild, out IRole role)
		{
			return guildToRoleMap.TryGetValue(guild, out role);
		}

		bool IsMatched(IActivity activity)
		{
			return
				activity != null && 
				(activities & (1 << (int)activity.Type)) != 0 &&
				Regex.IsMatch(activity.Name, nameRegex, RegexOptions.IgnoreCase);
		}
	}
	
	public class ActivityRoleManager
	{
		public bool IsEnabled { get; private set; }

		DiscordSocketClient client;

		List<ActivityDefinition> activities = new List<ActivityDefinition>();
		List<SocketGuild> guilds = new List<SocketGuild>();

		public ActivityRoleManager(DiscordSocketClient client)
		{
			this.client = client;
		}

		public async Task AddActivityRole(ActivityDefinition activity)
		{
			activities.Add(activity);

			foreach (SocketGuild guild in guilds)
			{
				activity.InitializeForGuild(guild);
			}

			if (IsEnabled)
			{
				foreach (SocketGuild guild in guilds)
				{
					await activity.ApplyRole(guild);
				}
			}
		}
				
		public async Task SetEnabled(bool isEnabled)
		{
			if (isEnabled != IsEnabled)
			{
				IsEnabled = isEnabled;

				if (isEnabled)
				{
					client.GuildAvailable += ApplyRolesAsync;
					client.GuildUnavailable += RemoveRolesAsync;

					client.GuildMemberUpdated += UserUpdatedAsync;
					
					foreach (SocketGuild guild in client.Guilds)
					{
						await ApplyRolesAsync(guild);
					}
				}
				else
				{
					client.GuildAvailable -= ApplyRolesAsync;
					client.GuildUnavailable -= RemoveRolesAsync;

					client.GuildMemberUpdated -= UserUpdatedAsync;
					
					foreach (SocketGuild guild in client.Guilds)
					{
						await RemoveRolesAsync(guild);
					}
				}
			}
		}

		async Task ApplyRolesAsync(SocketGuild guild)
		{
			Task _ = Task.Run(() => ApplyRoles(guild));

			await Task.CompletedTask;
		}

		async Task ApplyRoles(SocketGuild guild)
		{
			await guild.DownloadUsersAsync();

			foreach (ActivityDefinition activity in activities)
			{
				activity.InitializeForGuild(guild);

				//Not awaiting so they all go at once
				activity.ApplyRole(guild);
			}
		}

		async Task UserUpdatedAsync(SocketGuildUser previous, SocketGuildUser user)
		{
			Task _ = Task.Run(() => UserUpdated(user));

			await Task.CompletedTask;
		}

		async Task UserUpdated(SocketGuildUser user)
		{
			foreach (ActivityDefinition activity in activities)
			{
				await activity.ApplyRole(user);
			}
		}

		async Task RemoveRolesAsync(SocketGuild guild)
		{
			Task _ = Task.Run(() => ApplyRoles(guild));

			await Task.CompletedTask;
		}
		
		async Task RemoveRoles(SocketGuild guild)
		{
			foreach (ActivityDefinition activity in activities)
			{
				//Not awaiting so they all go at once
				activity.RemoveRole(guild);
			}
		}
	}
}

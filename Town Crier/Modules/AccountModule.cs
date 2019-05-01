using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using System.Collections.Generic;
using Discord.Rest;
using System.Linq;
using System;
using DiscordBot.Modules.ChatCraft;

using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Timers;
using Alta.WebApi.Models.DTOs.Responses;
using Discord.WebSocket;
using Alta.WebApi.Models;

namespace DiscordBot
{
	[Group("account")]
	public class AccountModule : CrierModuleBase
	{
		public class AccountDatabase
		{
			public Dictionary<ulong, AccountInfo> accounts = new Dictionary<ulong, AccountInfo>();

			public Dictionary<int, ulong> altaIdMap = new Dictionary<int, ulong>();

			public SortedSet<AccountInfo> expiryAccounts = new SortedSet<AccountInfo>(new AccountInfo.Comparer());
		}

		public class AccountInfo
		{
			public class Comparer : IComparer<AccountInfo>
			{
				public int Compare(AccountInfo x, AccountInfo y)
				{
					return x.supporterExpiry.CompareTo(y.supporterExpiry);
				}
			}

			public ulong discordIdentifier;
			public int altaIdentifier;
			public DateTime supporterExpiry;
			public bool isSupporter;
			public string username;
		}

		class VerifyData
		{
			public string discord;
		}

		static AccountDatabase database;
		static Timer timer;
		static bool isChanged;

		static SocketGuild guild;
		static SocketRole supporterRole;
		static SocketTextChannel supporterChannel;
		static SocketTextChannel generalChannel;

		static AccountModule()
		{
			timer = new Timer(10 * 60 * 1000);
			timer.Elapsed += UpdateAccounts;
			timer.Start();

			LoadDatabase();
		}

		public static void EnsureLoaded() { }

		static async void LoadDatabase()
		{
			while (guild == null)
			{
				guild = Program.GetGuild();
			}

			await guild.DownloadUsersAsync();

			supporterRole = guild.GetRole(547202953505800233);
			supporterChannel = guild.GetTextChannel(547204432144891907);
			generalChannel = guild.GetChannel(334933825383563266) as SocketTextChannel;

			try
			{
				database = FileDatabase.Read<AccountDatabase>("Accounts/accounts");

				Console.WriteLine("Loaded database!");

				foreach (AccountInfo account in database.accounts.Values)
				{
					await UpdateAsync(account, null);
				}

				Console.WriteLine(database.accounts.Count);
				Console.WriteLine(database.expiryAccounts.Count);
			}
			catch (Exception e)
			{
				Console.WriteLine("Account database not found! " + e.Message);

				database = new AccountDatabase();
			}
		}

		static async void UpdateAccounts(object sender, ElapsedEventArgs e)
		{
			if (database.expiryAccounts.Count != 0)
			{
				List<Task> tasks = new List<Task>();
				
				while (database.expiryAccounts.Count > 0)
				{
					AccountInfo first = database.expiryAccounts.Min;

					if (first.supporterExpiry < DateTime.UtcNow)
					{
						isChanged = true;

						database.expiryAccounts.Remove(first);

						tasks.Add(UpdateAsync(first, null));
					}
					else
					{
						break;
					}
				}

				await Task.WhenAll(tasks);
			}

			if (isChanged)
			{
				isChanged = false;

				Save();
			}
			else
			{
				Console.WriteLine("No account changes. No need to save.");
			}
		}
		

		static async Task UpdateAsync(AccountInfo account, SocketGuildUser user)
		{
			try
			{
				UserInfo userInfo = await ApiAccess.ApiClient.UserClient.GetUserInfoAsync(account.altaIdentifier);
				
				MembershipStatusResponse result = await ApiAccess.ApiClient.UserClient.GetMembershipStatus(account.altaIdentifier);

				if (userInfo == null)
				{
					Console.WriteLine("Couldn't find userinfo for " + account.username);
					return;
				}

				if (result == null)
				{
					Console.WriteLine("Couldn't find membership status for " + account.username);
					return;
				}
				
				account.supporterExpiry = result.ExpiryTime ?? DateTime.MinValue;
				account.isSupporter = result.IsMember;
				account.username = userInfo.Username;

				if (account.isSupporter)
				{
					database.expiryAccounts.Add(account);
				}
				
				if (user == null)
				{
					user = guild.GetUser(account.discordIdentifier);
				}

				if (user == null)
				{
					Console.WriteLine("Couldn't find Discord user for " + account.username + " " + account.discordIdentifier);
					return;
				}

				if (supporterRole == null)
				{
					supporterRole = guild.GetRole(547202953505800233);
					supporterChannel = guild.GetTextChannel(547204432144891907);
					generalChannel = guild.GetChannel(334933825383563266) as SocketTextChannel;
				}
				
				if (account.isSupporter)
				{
					if (user.Roles == null || !user.Roles.Contains(supporterRole))
					{
						try
						{
							await user.AddRoleAsync(supporterRole);
						}
						catch (Exception e)
						{
							Console.WriteLine("Error adding role");
							Console.WriteLine(user);
							Console.WriteLine(supporterRole);
						}

						await supporterChannel.SendMessageAsync($"{user.Mention} joined. Thanks for the support!");
						await generalChannel.SendMessageAsync($"{user.Mention} became a supporter! Thanks for the support!\nIf you'd like to find out more about supporting, visit https://townshiptale.com/supporter");
					}
				}
				else
				{
					await user.RemoveRoleAsync(supporterRole);
				}

				isChanged = true;
			}
			catch (Exception e)
			{
				Console.WriteLine("Error updating " + account.username);
				Console.WriteLine(e.Message);
			}
		}

		public static void Save()
		{
			isChanged = false;

			FileDatabase.Write("Accounts/accounts", database, true);
		}

		[Command("who")]
		[RequireUserPermission(GuildPermission.KickMembers)]
		public async Task Who(string username)
		{
			AccountInfo info = database.accounts.Values.FirstOrDefault(item => item.username == username);

			if (info != null)
			{
				await ReplyMentionAsync(username + " is " + guild.GetUser(info.discordIdentifier)?.Username);
			}
			else
			{
				await ReplyMentionAsync("Couldn't find " + username);
			}
		}

		[Command("update")]
		public async Task Update()
		{
			if (database.accounts.TryGetValue(Context.User.Id, out AccountInfo info))
			{
				await UpdateAsync(info, (SocketGuildUser)Context.User);

				await ReplyMentionAsync($"Hey {info.username}, your account info has been updated!");
			}
			else
			{
				await ReplyMentionAsync("You have not linked to an Alta account! To link, visit the 'Account Settings' page in the launcher.");
			}
		}


		[Command("forceupdate")]
		public async Task Update(IUser user)
		{
			if (database.accounts.TryGetValue(user.Id, out AccountInfo info))
			{
				await UpdateAsync(info, null);

				await ReplyMentionAsync($"{info.username}'s account info has been updated!");
			}
			else
			{
				await ReplyMentionAsync(user.Username + " have not linked to an Alta account!");
			}
		}


		[Command("unlink")]
		public async Task Unlink()
		{
			if (database.accounts.TryGetValue(Context.User.Id, out AccountInfo info))
			{
				database.accounts.Remove(Context.User.Id);
				database.expiryAccounts.Remove(info);
				database.altaIdMap.Remove(info.altaIdentifier);

				await ReplyMentionAsync("You are no longer linked to an Alta account!");
			}
			else
			{
				await ReplyMentionAsync("You have not linked to an Alta account! To link, visit the 'Account Settings' page in the launcher.");
			}
		}

		[Command(), Alias("linked")]
		public async Task IsLinked()
		{
			if (database.accounts.TryGetValue(Context.User.Id, out AccountInfo info))
			{
				await ReplyMentionAsync($"Hey {info.username}, your account is linked!");
			}
			else
			{
				await ReplyMentionAsync("You have not linked to an Alta account! To link, visit the 'Account Settings' page in the launcher.");
			}
		}

		[Command("verify")]
		public async Task Verify([Remainder]string encoded)
		{
			JwtSecurityToken token;
			Claim userData;
			Claim altaId;

			await DeleteCommand();

			try
			{
				token = new JwtSecurityToken(encoded);

				userData = token.Claims.FirstOrDefault(item => item.Type == "user_data");
				altaId = token.Claims.FirstOrDefault(item => item.Type == "UserId");
			}
			catch
			{
				await ReplyMentionAsync("Invalid verification token.");
				return;
			}

			if (userData == null || altaId == null)
			{
				await ReplyMentionAsync("Invalid verification token.");
			}
			else
			{
				try
				{
					VerifyData result = JsonConvert.DeserializeObject<VerifyData>(userData.Value);

					string test = result.discord.ToLower();
					string expected = Context.User.Username.ToLower() + "#" + Context.User.Discriminator;
					string alternate = Context.User.Username.ToLower() + " #" + Context.User.Discriminator;


					if (test != expected.ToLower() && test != alternate.ToLower())
					{
						await ReplyMentionAsync("Make sure you correctly entered your account info! You entered: " + result.discord + ". Expected: " + expected);
						return;
					}

					int id = int.Parse(altaId.Value);

					bool isValid = await ApiAccess.ApiClient.ServicesClient.IsValidShortLivedIdentityTokenAsync(token);

					if (isValid)
					{
						if (database.altaIdMap.TryGetValue(id, out ulong connected))
						{
							if (connected == Context.User.Id)
							{
								await ReplyMentionAsync("Already connected!");

								await UpdateAsync(database.accounts[connected], (SocketGuildUser)Context.User);
								return;
							}

							AccountInfo old = database.accounts[database.altaIdMap[id]];

							SocketGuildUser oldUser = Program.GetGuild().GetUser(old.discordIdentifier);

							await ReplyMentionAsync($"Unlinking your Alta account from {oldUser.Mention}...");
							
							database.accounts.Remove(database.altaIdMap[id]);
							database.expiryAccounts.Remove(old);
						}

						database.altaIdMap[id] = Context.User.Id;

						AccountInfo account;

						if (database.accounts.TryGetValue(Context.User.Id, out account))
						{
							await ReplyMentionAsync($"Unlinking your Discord from {account.username}...");
						}
						else
						{
							account = new AccountInfo()
							{
								discordIdentifier = Context.User.Id
							};

							database.accounts.Add(account.discordIdentifier, account);
						}

						account.altaIdentifier = id;

						await UpdateAsync(account, (SocketGuildUser)Context.User);

						await ReplyMentionAsync($"Successfully linked to your Alta account! Hey there {account.username}!");
						
						isChanged = true;
					}
					else
					{
						await ReplyMentionAsync("Invalid token! Try creating a new one!");
					}
				}
				catch (Exception e)
				{
					await ReplyMentionAsync("Invalid verification token : " + e.Message);
				}
			}
		}
	}
}
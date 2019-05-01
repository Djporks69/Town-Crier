using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Discord.Addons.Interactive;
using Jira.SDK;
using Jira.SDK.Domain;
using Jira.SDK.Tools;
using Discord.Rest;

namespace BugReporter
{
    public class JiraReporter : ModuleBase<SocketCommandContext>
    {
        public static JiraReportSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = JiraReportSettings.Load();
                }

                return settings;
            }
        }

        static JiraReportSettings settings;

        [Command("Version")]
        public async Task SetVersion(string version)
        {
            if (!Settings.CheckAllowed(Context.User))
            {
                return;
            }

            Settings.Version = version;
            Settings.Save();

            await ReplyAsync("Version changed to **" + version + "**.");
        }

        [Command("AddAllowedRole")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task AddRole(ulong Id)
        {
            Settings.AllowedRolesIDs.Add(Id);
            Settings.Save();
            Settings.AllowedRoles.Clear();

            await Context.User.SendMessageAsync("Role successfully added to the allowed list");
        }

        async Task<SocketMessage> WaitForReply(DiscordSocketClient client, IUserMessage sent, ulong channelId, ulong userId)
        {
            TaskCompletionSource<SocketMessage> eventTrigger = new TaskCompletionSource<SocketMessage>();

            CommandContext context = new CommandContext(client, sent);

            Func<SocketMessage, Task> handle = (SocketMessage response) =>
            {
                if (response.Channel.Id == channelId &&
                    response.Author.Id == userId)
                {
                    eventTrigger.SetResult(response);
                }

                return Task.FromResult(true);
            };

            client.MessageReceived += handle;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(new TimeSpan(0, 5, 0));
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            client.MessageReceived -= handle;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }
            else
            {
                return null;
            }
        }

        public void Report(IUser user, DiscordSocketClient client, RestUserMessage message, InteractiveService interactive, string type)
        {
            IDMChannel dmChannel = user.GetOrCreateDMChannelAsync().Result;

            IUserMessage sent = dmChannel.SendMessageAsync($"What is the title of this {type}?\n{message.Content}").Result;

            SocketMessage reply = WaitForReply(client, sent, dmChannel.Id, user.Id).Result;

            string title = reply?.Content;
            
            if (!string.IsNullOrEmpty(title))
            {
                Jira.SDK.Jira jira = new Jira.SDK.Jira();

                try
                {
                    jira.Connect(settings.JiraUrl, settings.Username, settings.Password);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                Project project = jira.GetProject(settings.JiraProject);

                StringBuilder stringBuilder = new StringBuilder();

                foreach (Attachment attachment in message.Attachments)
                {
                    stringBuilder.AppendLine(attachment.Url);
                }

                if (stringBuilder.ToString() == "")
                {
                    stringBuilder.AppendLine("* No attachments.");
                }

                try
                {
                    Issue newIssue = project.CreateIssue(new IssueFields()
                    {
                        Summary = title + " (USER)", //Set issue summary to the reply from the dev sent thru DMs.

                        IssueType = new IssueType(int.Parse(type == "Bug" ? settings.BugIssueType : settings.UserStory), type), //set issue-type

                        Description = "Issue reported by Discord user \""+message.Author.Username+"\". " +
                    $"*Reported bug*:\n{message.Content}\n*Attachments*:\n{stringBuilder.ToString()}", //Add bug description based on the reacted message

                    //    Labels = new List<string>()
                    //{
                    //    "User" //adds the "user" label
                    //},

                    //    CustomFields = new Dictionary<string, CustomField>()
                    //{
                    //    { "customfield_" + settings.CustomFieldId, new CustomField(int.Parse(settings.CustomFieldId), "Build")} //Adds custom field: build
                    //}
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

               IUserMessage confirmation = dmChannel.SendMessageAsync("Bug reported!").Result;
            }
        }
    }

    public class JiraReportSettings
    {
        [JsonIgnore]
        public Dictionary<IGuild, List<IRole>> AllowedRoles = new Dictionary<IGuild, List<IRole>>();

        public string Version { get; set; } = "VERSION";
        public List<ulong> AllowedRolesIDs = new List<ulong>();
        public ulong ServerID { get; set; } = 0;
        public string Username { get; set; } = "USERNAME";
        public string Password { get; set; } = "PASSWORD";
        public string JiraUrl { get; set; } = "JIRA URL";
        public string JiraProject { get; set; } = "JIRA PROJET KEY";
        public string BugIssueType { get; set; } = "Bug Issue Type ID";
        public string UserStory { get; set; } = "User Story Issue Type ID";
        public string CustomFieldId { get; set; } = "CUSTOM FIELD ID";

        public static JiraReportSettings Load()
        {
            if (!File.Exists(@"../../reporter.json"))
            {
                JiraReportSettings result = new JiraReportSettings();
                result.Save();

                return result;
            }

            return JsonConvert.DeserializeObject<JiraReportSettings>(File.ReadAllText(@"../../reporter.json"));
        }

        public bool CheckAllowed(SocketUser user)
        {
            SocketGuildUser guildUser = user as SocketGuildUser;

            IGuild guild = guildUser.Guild;

            if (!AllowedRoles.ContainsKey(guild))
            {
                List<IRole> allowed = new List<IRole>();

                foreach (ulong id in AllowedRolesIDs)
                {
                    allowed.Add(guild.GetRole(id));
                }

                AllowedRoles.Add(guild, allowed);
            }

            foreach (IRole role in AllowedRoles[guild])
            {
                if (guildUser.Roles.Contains(role))
                {
                    return true;
                }
            }

            return false;
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);

            File.WriteAllText(@"../../reporter.json", json);
        }
    }
}
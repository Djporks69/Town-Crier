# Town-Crier
A Township Tale's official Discord bot, written with Discord.NET

Join https://discord.gg/townshiptale to see Town Crier in action!

This is still early days, so still working out the kinks of how this will work on GitHub.

Who to talk to about stuff:
- Joel_Alta in the Discord linked above

Things that need some serious work:
- 	Program.cs - *throws up*, this whole project is what happens when someone hacks in things with very little thought as to where/how things are arranged.
- 	Right now ChatCraft's config file contains all game configuration (locations, items, etc.) as well as every 'player'. This means that the file is stupidly big on large severs, and the whole thing is loaded into RAM on startup.
- 	ChatCraft's player profile also has non-chatcraft related information, such as join date etc.
- 	Ideally 'player profiles' are moved to some form of database system.
- 	Ideally the game also isn't one huge file, but instead broken down in some way, to potentially allow for easier contribution of sets of items, locations, etc.


Hurdles that we need to work out:
-	The project relies on two internal projects (called WebApiClient and WebApiModels).
	These we have hooked up through Nuget to our private repository. I've included the DLL's in the repo manually.

	
Some other things to be vaguely aware of:
-	Chatty Township is half way through a rewrite, and the first version wasn't even completed.... So a lot of mess there
	Anything with !tc is semi-legacy and getting replaced
-	There's some automatic JIRA reporting code in there. It's not used, as I didn't have time to work it out.
	

Additional requirements for running:
- 	`token.txt` exists next to the executable (in `bin` folder?). 
	It's contents are `<DISCORD BOT TOKEN>`
- 	`account.txt` exists next to the exectuable (in `bin` folder?). 
	It's contents are `<ALTA USERNAME>|<ALTA PASSWORD>`
	
	
	
Random other information:
-	`reporter.json` goes somewhere if you want to look into that JIRA feature mentioned above.
	Content should be something like the following:

```json
{ 
  "AllowedRolesIDs": [ 
    416788657673076737, 
    334938548535033857 
  ], 
  "Version": "0.0.2.3", 
  "ServerID": 0, 
  "Username": "<email>", 
  "Password": "<password>", 
  "JiraUrl": "<jira URL>", 
  "JiraProject": "<jira project>", 
  "BugIssueType": "1", 
  "UserStory": "7", 
  "CustomFieldId": "0" 
}```
using DiscordBot.Modules.ChatCraft;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
	public static class FileDatabase
	{
		public static void Write(string target, object value, bool isBackingUp, params JsonConverter[] customConverters)
		{
			using (StreamWriter writer = new StreamWriter($"../../{target}.json"))
			{
				JsonSerializerSettings settings = new JsonSerializerSettings()
				{
					PreserveReferencesHandling = PreserveReferencesHandling.Objects
				};
				
				foreach (JsonConverter converter in customConverters)
				{
					settings.Converters.Add(converter);
				}

				string json = JsonConvert.SerializeObject(value, Formatting.Indented, settings);

				writer.Write(json);
			}

			Console.WriteLine($"{target} Saved");

			if (isBackingUp)
			{
				for (int i = 4; i > 0; i++)
				{
					try
					{
						System.IO.File.Copy($"../../{target} Backup {i}.json", $"../../{target} Backup {i + 1}.json", true);
					}
					catch { }
				}

				System.IO.File.Copy($"../../{target}.json", $"../../{target} Backup 1.json", true);

				Console.WriteLine($"{target} Backed up");
			}
		}

		internal static T Read<T>(string target, params JsonConverter[] customConverters)
		{
			using (StreamReader reader = new StreamReader($"../../{target}.json"))
			{
				string json = reader.ReadToEnd();

				JsonSerializerSettings settings = new JsonSerializerSettings() { PreserveReferencesHandling = PreserveReferencesHandling.Objects };

				foreach (JsonConverter converter in customConverters)
				{
					settings.Converters.Add(converter);
				}

				return JsonConvert.DeserializeObject<T>(json, settings);
			}
		}
	}
}

using Alta.WebApi.Client;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

public static class ApiAccess
{
	const int Timeout = 40;

	public static IHighLevelApiClient ApiClient { get; private set; }
	
	static SHA512 sha512 = new SHA512Managed();
	
	static ApiAccess()
	{
		StartWithEndpoint(HighLevelApiClientFactory.ProductionEndpoint);
	}

	public static void StartWithEndpoint(string endpoint)
	{
		if (ApiClient != null)
		{
			Console.WriteLine("Already have an Api Client");
			return;
		}

		SetApiClientLogging();

		ApiClient = HighLevelApiClientFactory.CreateHighLevelClient(endpoint, Timeout);
	}

	static void SetApiClientLogging()
	{
		//HighLevelApiClientFactory.SetLogging(new AltaLoggerFactory());
	}

	public static void StartOffline(LoginCredentials credentials)
	{
		if (ApiClient != null)
		{
			Console.WriteLine("Already have an Api Client");
			return;
		}

		SetApiClientLogging();

		ApiClient = HighLevelApiClientFactory.CreateOfflineHighLevelClient(credentials);
	}

	public async static Task EnsureLoggedIn()
	{
		if (!ApiClient.IsLoggedIn)
		{
			string[] account = System.IO.File.ReadAllText(System.IO.Directory.GetCurrentDirectory() + "/account.txt").Trim().Split('|');

			string username = account[0];
			string password = account[1];
			
			try
			{
				await ApiClient.LoginAsync(username, HashString(password));
				Console.WriteLine($"Logged in as {username}");
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}
	}

	static string HashString(string text)
	{
		//return Convert.ToBase64String(sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
		return BitConverter.ToString(sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", String.Empty).ToLowerInvariant();
	}
}
using System.Net.Sockets;
using System.Net;
using Polly.Timeout;
using Polly;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;

namespace QueryLib;

public class MasterServerClient
{
	//TODO? make this a configurable yml,json, or simple text file?
	private static readonly string[] _Masters = new string[]
	{
		"t1m1.pu.net", //bugs_
		"t1m2.pu.net", //bugs_
		"t1m3.pu.net", //bugs_
		"t1m1.tribes0.com",
		"63.228.117.246", //creamofwheat
		"t1m1.tribes1.co", //kigen
		//"tribes.lock-load.org", //lock-load
		"t1m1.tribesmasterserver.com", //or001
		"t1m1.tribes0.com", //plasmatic
		//"skbmaster.ath.cx", //SKB
	};

	private readonly string _masterServerIp;
	private readonly int _masterServerPort;

	public static async Task<List<GameServer>?> GetServersList()
	{
		var masters = await MasterServerClient.GetMasterEntries().ToListAsync();
		//Console.WriteLine(masters);

		IPEndPoint[] addresses = null;
        List<GameServer> servers = null;

		foreach (var m in masters)
		{
			var client = new MasterServerClient(m.AddressList.First().ToString(), 28000);
            
			var request = Task.Run(() =>
			{
				return client.RequestServerList().ToArray();
			});
			var timeout = Task.Delay(500);

            Debug.WriteLine($"using master {m.HostName} ({client._masterServerIp}:{client._masterServerPort})");

            await Task.WhenAny(request, timeout);

			//found a working master, use this one
			if (request.Result.Length > 0)
			{
                addresses = request.Result;
                servers = await QueryGameServersAsync(addresses, CancellationToken.None).ToListAsync();
                //servers = new List<GameServer>();
                //await foreach (var gameServer in QueryGameServersAsync(addresses, CancellationToken.None))
                //{
                //    // Process each game server (e.g., log, save, or handle the result)
                //    Debug.WriteLine($"Processed server at {gameServer.ServerAddress}");
                //    servers.Add(gameServer);
                //}

                break;
			}
		}
        servers = servers?.OrderByDescending(s => s.Players).ToList();
		return servers;
	}

	private static readonly TimeSpan _Timeout = TimeSpan.FromSeconds(3); //Timeout in seconds
	private static readonly TimeSpan _RateLimitDuration = TimeSpan.FromMinutes(1); // Visit no more than _MaxWebsites every X minutes
	private const int _RetryAttempts = 3; //How many retries to perform
	private const int _RetryBackoff = 2; //Backoff exponent in seconds
	private const int _RateLimitTasks = 50; //Limit to X concurrent tasks
	private const int _RateLimitQueue = 100; //Allow queuing up to 20 actions

	public static async IAsyncEnumerable<GameServer> QueryGameServersAsync(IPEndPoint[] serverAddresses, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
        Debug.WriteLine("QueryGameServersAsync()");
  //      var timeoutPolicy = Policy.TimeoutAsync(_Timeout);

		//var retryPolicy = Policy
		//	.Handle<Exception>() // Specify the exception(s) to handle
		//	.WaitAndRetryAsync(
		//		_RetryAttempts, // Retry up to X times
		//		retryAttempt => TimeSpan.FromSeconds(Math.Pow(_RetryBackoff, retryAttempt)), // Exponential backoff
		//		(exception, timeSpan, retryCount, context) =>
		//		{
		//			Console.WriteLine($"Retry {retryCount} in {timeSpan.TotalSeconds} seconds due to {exception.Message}");
		//		});

		//var policies = Policy.WrapAsync(retryPolicy, timeoutPolicy);

		//var rateLimitSemaphore = new SemaphoreSlim(_RateLimitQueue); // limit to X items

        Debug.WriteLine("made policy and semaphore");
        // Process each appId in batches respecting the rate limit
        foreach (var serverAddress in serverAddresses)
		{
            Debug.WriteLine("starting " + serverAddress.ToString());
            // Check if cancellation has been requested before processing the next appId
            cancellationToken.ThrowIfCancellationRequested();

			//await rateLimitSemaphore.WaitAsync(cancellationToken); // Pass the cancellation token

			// Call the method that handles processing with the token
			//var gameInfo = await GetGameInfoWithPoliciesAsync(serverAddress, policies, rateLimitSemaphore, cancellationToken);
            var gameInfo = GetGameInfo(serverAddress, cancellationToken);
            if (gameInfo != null)
			{
                Debug.WriteLine("yielding " + serverAddress.ToString());
                yield return gameInfo;
			}
		}
	}

	static async Task<GameServer> GetGameInfoWithPoliciesAsync(IPEndPoint serverAddress, IAsyncPolicy policies, SemaphoreSlim rateLimitSemaphore, CancellationToken cancellationToken)
	{
		try
		{
			// Execute the policies with the cancellation token
			return await policies.ExecuteAsync(ct => GetGameInfoAsync(serverAddress, ct), cancellationToken);
		}
		catch (TimeoutRejectedException)
		{
			Console.WriteLine($"Request for serverAddress {serverAddress} timed out.");
			return null;
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine($"Request for serverAddress {serverAddress} was cancelled.");
			return null; // Return null if the operation is cancelled
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error fetching data for serverAddress {serverAddress}: {ex.Message}");
			return null;
		}
		finally
		{
			// Ensure the semaphore is released even if an exception occurs
			rateLimitSemaphore.Release();
		}
	}

	// Example method for fetching game info (this would be implemented in your code)
	static async Task<GameServer> GetGameInfoAsync(IPEndPoint serverAddress, CancellationToken cancellationToken)
	{
		// Simulate async work with cancellation support
		//await Task.Delay(1000, cancellationToken); // Simulate a network call
        var gameServer = new GameServer(serverAddress);
        gameServer.Refresh();
        return gameServer;
        //return  // Replace with actual game info fetching logic
    }

    static GameServer GetGameInfo(IPEndPoint serverAddress, CancellationToken cancellationToken)
    {
        // Simulate async work with cancellation support
        //await Task.Delay(1000, cancellationToken); // Simulate a network call
        var gameServer = new GameServer(serverAddress);
        gameServer.Refresh();
        return gameServer;
        //return  // Replace with actual game info fetching logic
    }

    public MasterServerClient(string masterServerIp, int masterServerPort)
	{
		this._masterServerIp = masterServerIp;
		this._masterServerPort = masterServerPort;
	}

	public static async IAsyncEnumerable<IPHostEntry> GetMasterEntries()
	{
		//var masters = _Masters.ToAsyncEnumerable().SelectAwait(async s => await Dns.GetHostEntryAsync(s));
		var masters = _Masters.ToAsyncEnumerable().SelectAwait(async s =>
		{
			// Check if the string is a valid IP address
			if (IPAddress.TryParse(s, out var ipAddress))
			{
				// Create a manual IPHostEntry for the IP address
				return new IPHostEntry
				{
					HostName = s,
					AddressList = new[] { ipAddress }
				};
			}
			else
			{
				// Resolve the hostname
				return await Dns.GetHostEntryAsync(s);
			}
		});

		await foreach (var master in masters)
		{
			//yield return new IPEndPoint(master.AddressList.First(), 28000);
			//yield return new MasterServer(master.HostName, new IPEndPoint(master.AddressList.First(), 28000));
			yield return master;
		}
	}

	/*
	enum GameInfoPacketType
	{
		PingInfoQuery = 0x03,    // aka GAMESPY_QUERY in mstrsvr.h
		PingInfoResponse = 0x04,    //  "  GAMESVR_REPLY  "  "
		MasterServerHeartbeat = 0x05,    //  "  HEARTBT_MESSAGE
		MasterServerList = 0x06,    //  "  MSTRSVR_REPLY
		GameInfoQuery = 0x07,    //  "  GAMESVR_VERIFY
		GameInfoResponse = 0x08,
	};
	*/

	public IEnumerable<IPEndPoint> RequestServerList()
	{
		var udpClient = new UdpClient();
		try
		{
			udpClient.Connect(this._masterServerIp, this._masterServerPort);

			//these are other byte combinations i've seen from wireshark
			//note the 5th octet changes on each query- untested, but I think this may be
			//the client's count of how many queries its sent to master server
			//byte[] requestCommand1 = new byte[] { 0x10, 0x03, 0xff, 0x00, 0x12, 0x00, 0x00, 0x00 };
			//byte[] requestCommand2 = new byte[] { 0x10, 0x03, 0xff, 0x00, 0x03, 0x00, 0x00, 0x00 };
			//byte[] requestCommand3 = new byte[] { 0x10, 0x03, 0xff, 0x00, 0x93, 0x00, 0x00, 0x00 };
			//byte[] requestCommand4 = new byte[] { 0x10, 0x03, 0xff, 0x00, 0xe3, 0x00, 0x00, 0x00 };

			//this seems to be all we need. as far as I can tell, the bytes represent:
			//query version, PingInfoQuery, REQUEST_ALL_PACKETS
			byte[] requestCommand5 = new byte[] { 0x10, 0x03, 0xff };

			// Send the request
			udpClient.Send(requestCommand5, requestCommand5.Length);

			// Receive the response from the master server
			IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
			byte[] response = udpClient.Receive(ref remoteEndPoint);

			//handle response
			var results = ProcessResponse(response);
			//Console.WriteLine(results);
			return results;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
		finally
		{
			udpClient.Close();
		}

		return Enumerable.Empty<IPEndPoint>();
	}

	private IEnumerable<IPEndPoint> ProcessResponse(byte[] response)
	{
		//Console.WriteLine(response.Length);
		//TODO come up with actual rules to determine of too short
		if (response.Length < 22)
		{
			Console.WriteLine("Packet too short");
			return Enumerable.Empty<IPEndPoint>();
		}

		//Console.WriteLine(response);
		//master server response header is 8 bytes
		//var header = new Header(response[..8]);
		//Console.WriteLine(header);

		var byteSeparators = response.Select((value, index) => new { value, index })
						.Where(x => x.value == 0x06)
						.Select(x => x.index);
		//Console.WriteLine(byteSeparators);

		//skip first byte separator, since that is header and motd. the server list starts and the 2nd one (we hope!)
		//there could be more master server logic in here, but this works for now. also could be more going on if
		//there are more servers than the socket buffer, currently that's low since the game only has 37 servers
		//each server address is 7 bytes, see below for structure
		//byte 1 is the start separator 0x06
		//bytes 2-5 are the ip address
		//bytes 6-7 are the port
		var servers = byteSeparators.Skip(1).Select(s =>
		{
			var ipAddress = new IPAddress(response[(s + 1)..(s + 5)]);
			var port = BitConverter.ToUInt16(response, s + 5);
			//var port = (ushort)((response[offset] << 8) | response[offset + 1]);
			return new IPEndPoint(ipAddress, port);
		});

		return servers;
	}
}
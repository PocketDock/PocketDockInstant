using System.Net;
using System.Net.Sockets;
using System.Text;

public class QueryService
{
    private readonly int _port;
    private readonly string _ip;
    private UdpClient _udpClient;
    private Random _random;

    public QueryService(int port, string ip)
    {
        _port = port;
        _ip = ip;
    }

    public async Task<int> GetPlayers()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        if (_udpClient == null)
        {
            _udpClient = new UdpClient(_ip.Contains(':') ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
            _udpClient.Connect(_ip, _port);
            _random = new Random();
        }

        var sessionId = new byte[4];
        _random.NextBytes(sessionId);

        var challengeRequest = new byte[]
            {
                0xFE, 0xFD, //Magic
                0x09, //Type
            }
            .Concat(sessionId)
            .ToArray();
        Log("Sending ", challengeRequest);
        await _udpClient.SendAsync(challengeRequest, cts.Token);
        
        var challengeResponse = (await _udpClient.ReceiveAsync(cts.Token)).Buffer.Concat(new byte[] { 0x00 }).ToArray();
        Log("Received ", challengeResponse);
        VerifySessionId(sessionId, challengeResponse);
        var challenge = ToBigEndian(challengeResponse[5..^1]);

        var statRequest = new byte[]
            {
                0xFE, 0xFD, //Magic
                0x00, //Type 
            }
            .Concat(sessionId)
            .Concat(challenge)
            .Concat(new byte[] { 0x00 }) //NOTE: Needed to get PM to send a short response
            .ToArray();
        await _udpClient.SendAsync(statRequest, cts.Token);
        Log("Sending ", statRequest);

        Byte[] statResponse = (await _udpClient.ReceiveAsync(cts.Token)).Buffer;
        Log("Received ", statResponse);

        using var stream = new MemoryStream(statResponse);
        var reader = new BinaryReader(stream);
        reader.ReadBytes(5);
        var motd = reader.ReadNullTerminatedString();
        var gameType = reader.ReadNullTerminatedString();
        var map = reader.ReadNullTerminatedString();
        var numPlayers = reader.ReadNullTerminatedString();
        var maxPlayers = reader.ReadNullTerminatedString();
        var hostPort = reader.ReadInt16();
        var hostIp = reader.ReadNullTerminatedString();
        return int.Parse(numPlayers);
    }
    
    void VerifySessionId(ReadOnlySpan<byte> sessionId, ReadOnlySpan<byte> response)
    {
        var resSessionId = response[1..5];
        if (!sessionId.SequenceEqual(resSessionId))
        {
            throw new InvalidDataException("Session ID did not matched sent session");
        }
    }

    void Log(string msg, byte[] arr)
    {
        var za = arr.ToList().Select(x => Convert.ToHexString(new[] { x }));
        //Console.WriteLine(msg + string.Join(' ', za));
    }

    byte[] ToBigEndian(byte[] bytes)
    {
        var token = int.Parse(Encoding.ASCII.GetString(bytes));
        var newBytes = BitConverter.GetBytes(token);
        Array.Reverse(newBytes);
        return newBytes;
    }
}
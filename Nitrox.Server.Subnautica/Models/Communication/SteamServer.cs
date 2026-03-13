using System.Threading.Channels;
using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.AppEvents.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;
using Steamworks;

namespace Nitrox.Server.Subnautica.Models.Communication;

internal sealed class SteamServer : IHostedService, IPacketSender, IKickPlayer, ISessionCleaner
{
    private readonly Lock contextLock = new();
    private readonly ILogger<SteamServer> logger;
    private readonly IOptions<SubnauticaServerOptions> options;
    private readonly PacketRegistryService packetRegistryService;
    private readonly PacketSerializationService packetSerializationService;
    private readonly PlayerManager playerManager;
    private readonly SessionManager sessionManager;
    private readonly Channel<Task> taskChannel = Channel.CreateUnbounded<Task>();

    private bool isStarted;

    public SteamServer(
        PlayerManager playerManager,
        SessionManager sessionManager,
        PacketSerializationService packetSerializationService,
        PacketRegistryService packetRegistryService,
        IOptions<SubnauticaServerOptions> options,
        ILogger<SteamServer> logger
    )
    {
        this.playerManager = playerManager;
        this.sessionManager = sessionManager;
        this.packetSerializationService = packetSerializationService;
        this.packetRegistryService = packetRegistryService;
        this.options = options;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // UDP port for the spacewar server to do authentication on (ie, talk to Steam on)
        ushort AUTH_PORT = 8766;
        // UDP port for the spacewar server to listen on
        ushort SERVER_PORT = options.Value.ServerPort;

        // Don't let Steam do authentication
        EServerMode serverMode = EServerMode.eServerModeNoAuthentication;

        isStarted = GameServer.Init(0, SERVER_PORT, AUTH_PORT, serverMode, NitroxEnvironment.Version.ToString());

        if (!isStarted)
        {
            throw new Exception($"Failed to initialize Steam GameServer");
        }

        SteamGameServer.SetServerName("Nitrox");
        SteamGameServer.SetMaxPlayerCount(options.Value.MaxConnections);
        SteamGameServer.SetPasswordProtected(options.Value.IsPasswordRequired());
        SteamGameServer.SetDedicatedServer(false);
        SteamGameServer.SetModDir("Nitrox");
        SteamGameServer.SetProduct("NitroxServer");
        SteamGameServer.SetGameDescription("Nitrox Subnautica Server");

        // Anonymous logon since 
        SteamGameServer.LogOnAnonymous();

        var serverLoop = Task.Run(() =>
        {
            while (isStarted)
            {
                GameServer.RunCallbacks();
                Thread.Sleep(100);
            }
        }, );

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!isStarted)
        {
            return;
        }

        await SendPacketToAllAsync(new ServerStopped());

        try
        {
            await Task.Delay(100, CancellationToken.None); // Gives some time for the last few tasks to be queued up.
            taskChannel.Writer.TryComplete();
            await foreach (Task task in taskChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await task;
            }

            GameServer.Shutdown();
        }
        finally
        {
            isStarted = false;
        }
    }

    public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
    {
        return ValueTask.CompletedTask;
    }

    public async Task<bool> KickPlayer(SessionId sessionId, string reason = "")
    {
        await SendPacketAsync(new PlayerKicked(reason), sessionId);
        return true;
    }

    async Task IEvent<ISessionCleaner.Args>.OnEventAsync(ISessionCleaner.Args args)
    {
        Disconnect disconnect = new(args.Session.Id);
        await SendPacketToAllAsync(disconnect);
    }
}

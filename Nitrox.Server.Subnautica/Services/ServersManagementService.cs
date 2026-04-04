using System.Buffers;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Nitrox.Model.Constants;
using Nitrox.Model.MagicOnion;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Logging.Scopes;
using Nitrox.Server.Subnautica.Models.Logging.ZLogger;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Services;

/// <summary>
///     Connects to a locally running app that might want to track this server. Nitrox.Launcher is expected.
/// </summary>
internal sealed class ServersManagementService(PlayerManager playerManager, IPacketSender packetSender, CommandService commandProcessor, IOptions<ServerStartOptions> options, ILogger<ServersManagementService> logger) : BackgroundService
{
    public static readonly Channel<LogEntry> LogQueue = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CommandService commandProcessor = commandProcessor;
    private readonly ILogger<ServersManagementService> logger = logger;
    private readonly IOptions<ServerStartOptions> options = options;
    private readonly PlayerManager playerManager = playerManager;
    private GrpcChannel? channel;
    private string? channelIdentity;
    private Task? pushLogsTask;

    public override void Dispose() => channel?.Dispose();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerManagementReceiver? receiver = new(commandProcessor, packetSender);
        IServersManagement api = null;

        using PeriodicTimer refreshTimer = new(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                LauncherGrpcEndpoint? endpoint = await GetLauncherGrpcEndpointAsync();
                if (endpoint == null)
                {
                    await WaitNextAsync();
                    continue;
                }
                await RefreshConnectionAsync(endpoint.Value);

                // Push data
                await PushPollDataAsync(api);
                if (!pushLogsTask.IsBusyOrDone())
                {
                    pushLogsTask = CreateLoopingTask(PushLogsAsync, api, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                if (!ShouldIgnoreException(ex))
                {
                    logger.ZLogTrace($"{ex.Message}");
                }
            }
            await WaitNextAsync();
        }

        ValueTask<bool> WaitNextAsync() => refreshTimer.WaitForNextTickAsync(stoppingToken);

        async Task RefreshConnectionAsync(LauncherGrpcEndpoint endpoint)
        {
            if (channelIdentity != endpoint.Identity)
            {
                channel?.Dispose();
                channel = null;
                channelIdentity = null;
                if (api != null)
                {
                    await api.DisposeAsync();
                }
                api = null;
            }

            channel ??= CreateChannel(endpoint);
            channelIdentity ??= endpoint.Identity;
            if (api == null)
            {
                StreamingHubClientOptions grpcOptions = StreamingHubClientOptions.CreateWithDefault()
                                                                                 .WithCallOptions(new CallOptions(new Metadata
                                                                                 {
                                                                                     { "ProcessId", Environment.ProcessId.ToString() },
                                                                                     { "SaveName", options.Value.SaveName }
                                                                                 }));
                api = await StreamingHubClient.ConnectAsync<IServersManagement, IServerManagementReceiver>(channel,
                                                                                                           receiver,
                                                                                                           cancellationToken: stoppingToken,
                                                                                                           options: grpcOptions);
            }
        }

        Task CreateLoopingTask(Func<IServersManagement, CancellationToken, Task> action, IServersManagement service, CancellationToken cancellationToken) =>
            Task.Run(async () =>
            {
                try
                {
                    await action(service, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (!ShouldIgnoreException(ex))
                    {
                        logger.ZLogError(ex, $"Error during looping task");
                    }
                }
            }, cancellationToken);

        static GrpcChannel CreateChannel(LauncherGrpcEndpoint endpoint)
        {
            if (endpoint.PipeName is not null)
            {
                SocketsHttpHandler handler = new()
                {
                    ConnectCallback = async (_, cancellationToken) =>
                    {
                        NamedPipeClientStream pipeStream = new(".", endpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        await pipeStream.ConnectAsync(cancellationToken);
                        return pipeStream;
                    }
                };

                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
            }

            return GrpcChannel.ForAddress($"http://127.0.0.1:{endpoint.Port}");
        }
    }

    private async Task PushPollDataAsync(IServersManagement api)
    {
        await api.SetPlayerCount(playerManager.PlayerCount);
    }

    private async Task PushLogsAsync(IServersManagement api, CancellationToken cancellationToken)
    {
        await foreach (LogEntry log in LogQueue.Reader.ReadAllAsync(cancellationToken))
        {
            string category = log.Entry.LogInfo.Category.ToString();
            DateTimeOffset time = log.Entry.LogInfo.Timestamp.Local;
            int level = log.Entry.LogInfo.LogLevel switch
            {
                LogLevel.Information => 0,
                LogLevel.Debug => 1,
                LogLevel.Warning => 2,
                LogLevel.Error => 3,
                _ => 0
            };
            bool isPlain = log.Entry.TryGetProperty(out PlainScope _);
            string? message = log.Generator(log.Entry, log.Formatter, log.Writer); // Generator will dispose of the log data, so this needs to be called "last".
            if (message is "")
            {
                continue;
            }
            // Omit last "new line" occurrence, as it is implied.
            if (message.LastIndexOf(Environment.NewLine, StringComparison.Ordinal) is var newlineIndex and > -1)
            {
                message = message.Substring(0, newlineIndex);
            }

            await api.AddOutputLine(category, isPlain ? null : time, level, message);
        }
    }

    private bool ShouldIgnoreException(Exception ex)
    {
        ex = ex is AggregateException aggregate ? aggregate.InnerException : ex;
        return ex switch
        {
            RpcException { Status.StatusCode: StatusCode.Unavailable or StatusCode.Cancelled } => true,
            OperationCanceledException => true,
            _ => false
        };
    }

    private async Task<LauncherGrpcEndpoint?> GetLauncherGrpcEndpointAsync()
    {
        try
        {
            string endpointDescriptor = await File.ReadAllTextAsync(Path.Combine(Path.GetTempPath(), LauncherConstants.GRPC_LISTEN_PORT_TEMP_FILE_NAME));
            endpointDescriptor = endpointDescriptor.Trim();

            if (endpointDescriptor.StartsWith(LauncherConstants.GRPC_NAMED_PIPE_ENDPOINT_PREFIX, StringComparison.Ordinal))
            {
                string pipeName = endpointDescriptor.Substring(LauncherConstants.GRPC_NAMED_PIPE_ENDPOINT_PREFIX.Length);
                if (!string.IsNullOrWhiteSpace(pipeName))
                {
                    return LauncherGrpcEndpoint.NamedPipe(pipeName);
                }
            }

            if (int.TryParse(endpointDescriptor, out int port))
            {
                return LauncherGrpcEndpoint.TcpPort(port);
            }

            logger.ZLogWarningOnce($"Unable to parse launcher gRPC endpoint metadata. Retrying...");
        }
        catch (Exception)
        {
            logger.ZLogWarningOnce($"Unable to get gRPC listen port from Nitrox Launcher, it might not be running. Retrying...");
        }
        return null;
    }

    private readonly record struct LauncherGrpcEndpoint(int? Port, string? PipeName)
    {
        public string Identity => PipeName is null ? $"tcp:{Port}" : $"pipe:{PipeName}";

        public static LauncherGrpcEndpoint TcpPort(int port) => new(port, null);

        public static LauncherGrpcEndpoint NamedPipe(string pipeName) => new(null, pipeName);
    }

    private class ServerManagementReceiver(CommandService commandProcessor, IPacketSender packetSender) : IServerManagementReceiver
    {
        public void OnCommand(string command) => commandProcessor.ExecuteCommand(command, new HostToServerCommandContext(packetSender), out _);
    }

    internal record LogEntry(IZLoggerEntry Entry, IZLoggerFormatter Formatter, ZLoggerPlainOptions.LogGeneratorCall Generator, ArrayBufferWriter<byte> Writer);
}

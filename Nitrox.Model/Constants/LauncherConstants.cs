namespace Nitrox.Model.Constants;

public static class LauncherConstants
{
    public const string GRPC_LISTEN_PORT_TEMP_FILE_NAME = "nitrox-launcher-grpc-port.txt";
    public const string GRPC_NAMED_PIPE_ENDPOINT_PREFIX = "pipe:";
    public const string GRPC_NAMED_PIPE_NAME_PREFIX = "nitrox-launcher-grpc-";

    public static string GetGrpcNamedPipeName(int processId) => $"{GRPC_NAMED_PIPE_NAME_PREFIX}{processId}";
}

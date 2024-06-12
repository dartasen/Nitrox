﻿global using NitroxModel.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NitroxModel.Core;
using NitroxModel.DataStructures;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.DataStructures.Util;
using NitroxModel.Helper;
using NitroxModel.Platforms.OS.Shared;
using NitroxServer;
using NitroxServer_Subnautica.Communication;
using NitroxServer.ConsoleCommands.Processor;

namespace NitroxServer_Subnautica;

[SuppressMessage("Usage", "DIMA001:Dependency Injection container is used directly")]
public class Program
{
    private static readonly Dictionary<string, Assembly> resolvedAssemblyCache = new();
    private static Lazy<string> gameInstallDir;
    private static readonly CircularBuffer<string> inputHistory = new(1000);
    private static int currentHistoryIndex;

    private static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomainOnAssemblyResolve;

        await StartServer(args);
    }

    /// <summary>
    ///     Initialize server here so that the JIT can compile the EntryPoint method without having to resolve dependencies
    ///     that require the <see cref="AppDomain.AssemblyResolve" /> handler.
    /// </summary>
    /// <remarks>
    ///     https://stackoverflow.com/a/6089153/1277156
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task StartServer(string[] args)
    {
        Action<string> ConsoleCommandHandler()
        {
            ConsoleCommandProcessor commandProcessor = null;
            return submit =>
            {
                try
                {
                    commandProcessor ??= NitroxServiceLocator.LocateService<ConsoleCommandProcessor>();
                }
                catch (Exception)
                {
                    // ignored
                }
                commandProcessor?.ProcessCommand(submit, Optional.Empty, Perms.CONSOLE);
            };
        }

        // The thread that writers to console is paused while selecting text in console. So console writer needs to be async.
        Log.Setup(true, isConsoleApp: true);
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        CultureManager.ConfigureCultureInfo();
        if (!Console.IsInputRedirected)
        {
            Console.TreatControlCAsInput = true;
        }
        
        Log.Info($"Starting NitroxServer {NitroxEnvironment.ReleasePhase} v{NitroxEnvironment.Version} for Subnautica");

        Server server;
        Task handleConsoleInputTask;
        CancellationTokenSource cancellationToken = new();
        try
        {
            handleConsoleInputTask = HandleConsoleInputAsync(ConsoleCommandHandler(), cancellationToken);
            AppMutex.Hold(() => Log.Info("Waiting on other Nitrox servers to initialize before starting.."), 120000);
            
            Stopwatch watch = Stopwatch.StartNew();
            
            // Allow game path to be given as command argument
            string gameDir = "";
            if (args.Length > 0 && Directory.Exists(args[0]) && File.Exists(Path.Combine(args[0], "Subnautica.exe")))
            {
                gameDir = Path.GetFullPath(args[0]);
                gameInstallDir = new Lazy<string>(() => gameDir);
            }
            else
            {
                gameInstallDir = new Lazy<string>(() =>
                {
                    gameDir = NitroxUser.GamePath;
                    return gameDir;
                });
            }
            Log.Info($"Using game files from: {gameDir}");

            NitroxServiceLocator.InitializeDependencyContainer(new SubnauticaServerAutoFacRegistrar());
            NitroxServiceLocator.BeginNewLifetimeScope();
            server = NitroxServiceLocator.LocateService<Server>();
            
            Log.SaveName = server.Name;

            await WaitForAvailablePortAsync(server.Port);

            if (!server.Start(cancellationToken) && !cancellationToken.IsCancellationRequested)
            {
                throw new Exception("Unable to start server.");
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                watch.Stop();
            }
            else
            {
                watch.Stop();
                Log.Info($"Server started ({Math.Round(watch.Elapsed.TotalSeconds, 1)}s)");
                Log.Info("To get help for commands, run help in console or /help in chatbox");
            }
        }
        finally
        {
            // Allow other servers to start initializing.
            AppMutex.Release();
        }

        await handleConsoleInputTask;

        Console.WriteLine($"{Environment.NewLine}Server is closing..");
    }

    /// <summary>
    ///     Handles per-key input of the console and passes input submit to <see cref="ConsoleCommandProcessor" />.
    /// </summary>
    private static async Task HandleConsoleInputAsync(Action<string> submitHandler, CancellationTokenSource cancellation = default)
    {
        cancellation ??= new CancellationTokenSource();

        ConcurrentQueue<string> commandQueue = new();

        if (Console.IsInputRedirected)
        {
            Task.Run(() =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    string commandRead = Console.ReadLine();
                    commandQueue.Enqueue(commandRead);
                }
            }, cancellation.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error(t.Exception);
                }
            });
        }
        else
        {
            StringBuilder inputLineBuilder = new();

            void ClearInputLine()
            {
                currentHistoryIndex = 0;
                inputLineBuilder.Clear();
                Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r");
            }

            void RedrawInput(int start = 0, int end = 0)
            {
                int lastPosition = Console.CursorLeft;
                // Expand range to end if end value is -1
                if (start > -1 && end == -1)
                {
                    end = Math.Max(inputLineBuilder.Length - start, 0);
                }

                if (start == 0 && end == 0)
                {
                    // Redraw entire line
                    Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r{inputLineBuilder}");
                }
                else
                {
                    // Redraw part of line
                    string changedInputSegment = inputLineBuilder.ToString(start, end);
                    Console.CursorVisible = false;
                    Console.Write($"{changedInputSegment}{new string(' ', inputLineBuilder.Length - changedInputSegment.Length - Console.CursorLeft + 1)}");
                    Console.CursorVisible = true;
                }
                Console.CursorLeft = lastPosition;
            }

            Task.Run(async () =>
            {
                while (!cancellation?.IsCancellationRequested ?? false)
                {
                    if (!Console.KeyAvailable)
                    {
                        try
                        {
                            await Task.Delay(10, cancellation.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            // ignored
                        }
                        continue;
                    }

                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    // Handle (ctrl) hotkeys
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.C:
                                if (inputLineBuilder.Length > 0)
                                {
                                    ClearInputLine();
                                    continue;
                                }

                                await cancellation.CancelAsync();
                                return;
                            case ConsoleKey.D:
                                await cancellation.CancelAsync();
                                return;
                            default:
                                // Unhandled modifier key
                                continue;
                        }
                    }

                    if (keyInfo.Modifiers == 0)
                    {
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.LeftArrow when Console.CursorLeft > 0:
                                Console.CursorLeft--;
                                continue;
                            case ConsoleKey.RightArrow when Console.CursorLeft < inputLineBuilder.Length:
                                Console.CursorLeft++;
                                continue;
                            case ConsoleKey.Backspace:
                                if (inputLineBuilder.Length > Console.CursorLeft - 1 && Console.CursorLeft > 0)
                                {
                                    inputLineBuilder.Remove(Console.CursorLeft - 1, 1);
                                    Console.CursorLeft--;
                                    Console.Write(' ');
                                    Console.CursorLeft--;
                                    RedrawInput();
                                }
                                continue;
                            case ConsoleKey.Delete:
                                if (inputLineBuilder.Length > 0 && Console.CursorLeft < inputLineBuilder.Length)
                                {
                                    inputLineBuilder.Remove(Console.CursorLeft, 1);
                                    RedrawInput(Console.CursorLeft, inputLineBuilder.Length - Console.CursorLeft);
                                }
                                continue;
                            case ConsoleKey.Home:
                                Console.CursorLeft = 0;
                                continue;
                            case ConsoleKey.End:
                                Console.CursorLeft = inputLineBuilder.Length;
                                continue;
                            case ConsoleKey.Escape:
                                ClearInputLine();
                                continue;
                            case ConsoleKey.Tab:
                                if (Console.CursorLeft + 4 < Console.WindowWidth)
                                {
                                    inputLineBuilder.Insert(Console.CursorLeft, "    ");
                                    RedrawInput(Console.CursorLeft, -1);
                                    Console.CursorLeft += 4;
                                }
                                continue;
                            case ConsoleKey.UpArrow when inputHistory.Count > 0 && currentHistoryIndex > -inputHistory.Count:
                                inputLineBuilder.Clear();
                                inputLineBuilder.Append(inputHistory[--currentHistoryIndex]);
                                RedrawInput();
                                Console.CursorLeft = Math.Min(inputLineBuilder.Length, Console.WindowWidth);
                                continue;
                            case ConsoleKey.DownArrow when inputHistory.Count > 0 && currentHistoryIndex < 0:
                                if (currentHistoryIndex == -1)
                                {
                                    ClearInputLine();
                                    continue;
                                }
                                inputLineBuilder.Clear();
                                inputLineBuilder.Append(inputHistory[++currentHistoryIndex]);
                                RedrawInput();
                                Console.CursorLeft = Math.Min(inputLineBuilder.Length, Console.WindowWidth);
                                continue;
                        }
                    }
                    // Handle input submit to submit handler
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        string submit = inputLineBuilder.ToString();
                        if (inputHistory.Count == 0 || inputHistory[inputHistory.LastChangedIndex] != submit)
                        {
                            inputHistory.Add(submit);
                        }
                        currentHistoryIndex = 0;
                        commandQueue.Enqueue(submit);
                        inputLineBuilder.Clear();
                        Console.WriteLine();
                        continue;
                    }

                    // If unhandled key, append as input.
                    if (keyInfo.KeyChar != 0)
                    {
                        Console.Write(keyInfo.KeyChar);
                        if (Console.CursorLeft - 1 < inputLineBuilder.Length)
                        {
                            inputLineBuilder.Insert(Console.CursorLeft - 1, keyInfo.KeyChar);
                            RedrawInput(Console.CursorLeft, -1);
                        }
                        else
                        {
                            inputLineBuilder.Append(keyInfo.KeyChar);
                        }
                    }
                }
            }, cancellation.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error(t.Exception);
                }
            });
        }

        using IpcHost ipcHost = IpcHost.StartReadingCommands(command => commandQueue.Enqueue(command));

        // Important: keep command handler on the main thread (i.e. don't Task.Run)
        while (!cancellation.IsCancellationRequested)
        {
            while (commandQueue.TryDequeue(out string command))
            {
                submitHandler(command);
            }
            await Task.Delay(10);
        }
    }

    private static async Task WaitForAvailablePortAsync(int port, int timeoutInSeconds = 30)
    {
        int messageLength = 0;
        void PrintPortWarn(int timeRemaining)
        {
            string message = $"Port {port} UDP is already in use. Please change the server port or close out any program that may be using it. Retrying for {timeRemaining} seconds until it is available...";
            messageLength = message.Length;
            Log.Warn(message);
        }

        Validate.IsTrue(timeoutInSeconds >= 5, "Timeout must be at least 5 seconds.");

        DateTimeOffset time = DateTimeOffset.UtcNow;
        bool first = true;
        using CancellationTokenSource source = new(timeoutInSeconds * 1000);

        try
        {
            while (true)
            {
                source.Token.ThrowIfCancellationRequested();
                IPEndPoint endPoint = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().FirstOrDefault(ip => ip.Port == port);
                if (endPoint == null)
                {
                    break;
                }

                if (first)
                {
                    first = false;
                    PrintPortWarn(timeoutInSeconds);
                }
                else if (Environment.UserInteractive)
                {
                    // If not first time, move cursor up the number of lines it takes up to overwrite previous message
                    int numberOfLines = (int)Math.Ceiling( ((double)messageLength + 15) / Console.BufferWidth );
                    for (int i = 0; i < numberOfLines; i++)
                    {
                        if (Console.CursorTop > 0) // Check to ensure we don't go out of bounds
                        {
                            Console.CursorTop--;
                        }
                    }
                    Console.CursorLeft = 0;
                    
                    PrintPortWarn(timeoutInSeconds - (DateTimeOffset.UtcNow - time).Seconds);
                }

                await Task.Delay(500, source.Token);
            }
        }
        catch (OperationCanceledException ex)
        {
            Log.Error(ex, "Port availability timeout reached.");
            throw;
        }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex);
        }

        if (!Environment.UserInteractive || Console.In == StreamReader.Null)
        {
            return;
        }

        string mostRecentLogFile = Log.GetMostRecentLogFile();
        if (mostRecentLogFile == null)
        {
            return;
        }

        Log.Info("Press L to open log file before closing. Press any other key to close . . .");
        ConsoleKeyInfo key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.L)
        {
            Log.Info($"Opening log file at: {mostRecentLogFile}..");
            using Process process = FileSystem.Instance.OpenOrExecuteFile(mostRecentLogFile);
        }

        Environment.Exit(1);
    }

    private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        string dllFileName = args.Name.Split(',')[0];
        if (!dllFileName.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
        {
            dllFileName += ".dll";
        }
        // If available, return cached assembly
        if (resolvedAssemblyCache.TryGetValue(dllFileName, out Assembly val))
        {
            return val;
        }

        // Load DLLs where this program (exe) is located
        string dllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "lib", dllFileName);
        // Prefer to use Newtonsoft dll from game instead of our own due to protobuf issues. TODO: Remove when we do our own deserialization of game data instead of using the game's protobuf.
        if (dllPath.IndexOf("Newtonsoft.Json.dll", StringComparison.OrdinalIgnoreCase) >= 0 || !File.Exists(dllPath))
        {
            // Try find game managed libraries
            dllPath = Path.Combine(gameInstallDir.Value, "Subnautica_Data", "Managed", dllFileName);
        }

        // Read assemblies as bytes as to not lock the file so that Nitrox can patch assemblies while server is running.
        Assembly assembly = Assembly.Load(File.ReadAllBytes(dllPath));
        return resolvedAssemblyCache[dllFileName] = assembly;
    }
}

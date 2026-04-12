using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.OS.MacOS;
using Nitrox.Model.Platforms.OS.Unix;
using Nitrox.Model.Platforms.OS.Windows;

namespace Nitrox.Model.Platforms.OS.Shared;

public class ProcessEx : IDisposable
{
    private readonly ProcessExBase implementation;

    public int Id => implementation.Id;
    public string? Name => implementation.Name;
    public IntPtr Handle => implementation.Handle;
    public ProcessModuleEx MainModule => implementation.MainModule;
    public string? MainModuleFileName => implementation.MainModuleFileName;
    public IntPtr MainWindowHandle => implementation.MainWindowHandle;
    public string? MainWindowTitle => implementation.MainWindowTitle;

    /// <summary>
    ///     True if process is running and in a recoverable state.
    /// </summary>
    public bool IsRunning => implementation.IsRunning;

    public ProcessEx(int pid)
    {
        implementation = ProcessExFactory.Create(pid);
    }

    private ProcessEx(ProcessExBase implementation)
    {
        Validate.NotNull(implementation, nameof(implementation));
        this.implementation = implementation;
    }

    public static ProcessEx? From(Process? process)
    {
        if (process == null || process.HasExited)
        {
            return null;
        }

        return new ProcessEx(ProcessExFactory.Create(process));
    }

    public static ProcessEx? From(ProcessStartInfo startInfo) => From(Process.Start(startInfo));

    public static bool ProcessExists(string procName, Func<ProcessEx, bool>? predicate = null)
    {
        ProcessEx proc = null;
        try
        {
            proc = GetFirstProcess(procName, predicate);
            return proc != null;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            proc?.Dispose();
        }
    }

    public static ProcessEx? Start(string fileName, IEnumerable<(string, string)>? environmentVariables = null, string? workingDirectory = null, string? commandLine = null, bool createWindow = true)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = !createWindow
        };
        if (workingDirectory != null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (environmentVariables != null)
        {
            foreach ((string key, string value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        if (!string.IsNullOrEmpty(commandLine))
        {
            startInfo.Arguments = commandLine;
        }

        return From(startInfo);
    }

#if NET
    public static Process? StartProcessDetached(ProcessStartInfo startInfo)
    {
        if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            throw new NotSupportedException($"Arguments must be supplied via {startInfo.ArgumentList}");
        }

        // On Linux, processes are started as child by default. So we wrap as shell command to start detached from current process.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            List<string> newArgs = ["-c", string.Join(" ", "nohup", $"'{startInfo.FileName}'", string.Join(" ", startInfo.ArgumentList.Select(a => $"'{a}'")), ">/dev/null 2>&1", "&")];
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Clear();
            foreach (string arg in newArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        return Process.Start(startInfo);
    }

    /// <summary>
    ///     Starts the current app as a new instance.
    /// </summary>
    public static void StartSelf(params string[] arguments)
    {
        string executableFilePath = Helper.NitroxUser.ExecutableFilePath ?? Environment.ProcessPath;
        // On Linux, entry assembly is .dll file but real executable is without extension.
        string temp = Path.ChangeExtension(executableFilePath, null);
        if (File.Exists(temp))
        {
            executableFilePath = temp;
        }
        temp = Path.ChangeExtension(executableFilePath, ".exe");
        if (File.Exists(temp))
        {
            executableFilePath = temp;
        }

        using Process proc = StartProcessDetached(new ProcessStartInfo(executableFilePath!, arguments));
    }

    /// <summary>
    ///     Starts the current app as a new instance, passing the same command line arguments.
    /// </summary>
    public static void StartSelfCopyArgs() => StartSelf(NitroxEnvironment.CommandLineArgs);
#endif

    /// <summary>
    ///     Opens the URI in the default browser. Forces the URI scheme as HTTPS if given as HTTP.
    /// </summary>
    public static void OpenUri(string uri)
    {
        UriBuilder builder = new(uri);
        if (builder.Scheme == Uri.UriSchemeHttps || builder.Scheme == Uri.UriSchemeHttp)
        {
            builder.Scheme = Uri.UriSchemeHttps;
            if (builder.Port is 80 or 443)
            {
                builder.Port = -1;
            }
        }
        using Process proc = Process.Start(new ProcessStartInfo
        {
            FileName = builder.Uri.ToString(),
            UseShellExecute = true,
            Verb = "open"
        });
    }

    /// <summary>
    ///     Opens a directory in the default OS directory viewer.
    /// </summary>
    /// <param name="directory">Directory to open</param>
    /// <returns>True if directory opened successfully, false if directory does not exist.</returns>
    public static bool OpenDirectory(string? directory)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        using Process? proc = Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            Verb = "open",
            UseShellExecute = true
        });

        return true;
    }

    public static ProcessEx? GetFirstProcess(string procName, Func<ProcessEx, bool>? predicate = null)
    {
        ProcessEx? found = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            procName = Path.GetFileNameWithoutExtension(procName);
        }

        foreach (Process proc in Process.GetProcessesByName(procName))
        {
            // Already found, dispose all other process handles.
            if (found != null)
            {
                proc.Dispose();
                continue;
            }

            ProcessEx procEx = From(proc);
            if (procEx != null && predicate != null && !predicate(procEx))
            {
                procEx.Dispose();
                continue;
            }

            found = procEx;
        }

        return found;
    }

    public static IEnumerable<T> GetProcessesByName<T>(string procName, Func<ProcessEx, T> selector)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            procName = Path.GetFileNameWithoutExtension(procName);
        }

        foreach (Process proc in Process.GetProcessesByName(procName))
        {
            ProcessEx procEx = From(proc);
            T result;
            try
            {
                result = selector(procEx);
                if (result is not Process or ProcessEx)
                {
                    procEx?.Dispose();
                }
            }
            catch
            {
                continue;
            }
            yield return result;
        }
    }

    public byte[] ReadMemory(IntPtr address, int size) => implementation.ReadMemory(address, size);

    public int WriteMemory(IntPtr address, byte[] data) => implementation.WriteMemory(address, data);

    public IEnumerable<ProcessModuleEx> GetModules() => implementation.GetModules();

    public void Suspend() => implementation.Suspend();

    public void Resume() => implementation.Resume();

    public void Terminate() => implementation.Terminate();

    public void Dispose() => implementation.Dispose();
}

public abstract class ProcessExBase : IDisposable
{
    protected readonly Process? Process;

    public virtual int Id => Process?.Id ?? -1;
    public virtual string? Name => Process?.ProcessName;
    public virtual IntPtr Handle => Process?.Handle ?? IntPtr.Zero;
    public abstract ProcessModuleEx MainModule { get; }
    public virtual string? MainModuleFileName => Process?.MainModule?.FileName;
    public virtual IntPtr MainWindowHandle => Process?.MainWindowHandle ?? IntPtr.Zero;
    public virtual string? MainWindowTitle => Process?.MainWindowTitle;

    public virtual bool IsRunning
    {
        get
        {
            if (Process == null)
            {
                return true;
            }

            Process.Refresh();

            if (!Process.HasExited || Process.Responding)
            {
                return true;
            }

            return false;
        }
    }

    protected ProcessExBase(int id)
    {
        try
        {
            Process = Process.GetProcessById(id);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    protected ProcessExBase(Process process)
    {
        Process = process;
    }

    public abstract byte[] ReadMemory(IntPtr address, int size);

    public abstract int WriteMemory(IntPtr address, byte[] data);

    public abstract IEnumerable<ProcessModuleEx> GetModules();

    public abstract void Suspend();

    public abstract void Resume();

    public abstract void Terminate();

    public static bool IsElevated()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return WindowsProcessEx.IsElevated();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return UnixProcessEx.IsElevated();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return MacProcessEx.IsElevated();
        }

        throw new PlatformNotSupportedException($"'{RuntimeInformation.OSDescription}' is not supported");
    }

    public virtual void Dispose()
    {
        Process?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ProcessModuleEx
{
    public IntPtr BaseAddress { get; set; }

    public string? ModuleName { get; set; }

    public string? FileName { get; set; }

    public int ModuleMemorySize { get; set; }
}

public static class ProcessExFactory
{
    public static ProcessExBase Create(int pid)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsProcessEx(pid);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new UnixProcessEx(pid);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacProcessEx(pid);
        }

        throw new PlatformNotSupportedException($"'{RuntimeInformation.OSDescription}' is not supported");
    }

    public static ProcessExBase Create(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsProcessEx(process);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new UnixProcessEx(process);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacProcessEx(process);
        }
        throw new PlatformNotSupportedException($"'{RuntimeInformation.OSDescription}' is not supported");
    }
}

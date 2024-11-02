using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NitroxModel.Platforms.OS.MacOS;
using NitroxModel.Platforms.OS.Unix;
using NitroxModel.Platforms.OS.Windows;

namespace NitroxModel.Platforms.OS.Shared;

public class ProcessEx : IDisposable
{
    private readonly ProcessExBase implementation;

    public int Id => implementation.Id;
    public string Name => implementation.Name;
    public IntPtr Handle => implementation.Handle;
    public ProcessModuleEx MainModule => implementation.MainModule;
    public string MainModuleFileName => implementation.MainModuleFileName;
    public IntPtr MainWindowHandle => implementation.MainWindowHandle;

    public ProcessEx(int pid)
    {
        implementation = ProcessExFactory.Create(pid);
    }

    public ProcessEx(Process process)
    {
        implementation = ProcessExFactory.Create(process.Id);
    }

    public static bool ProcessExists(string procName, Func<ProcessEx, bool> predicate = null)
    {
        ProcessEx proc = null;
        try
        {
            proc = GetFirstProcess(procName, predicate);
            return proc != null;
        }
        finally
        {
            proc?.Dispose();
        }
    }

    public static ProcessEx Start(string fileName = null, IEnumerable<(string, string)> environmentVariables = null, string workingDirectory = null, string commandLine = null, bool createWindow = true)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = !createWindow,
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        if (!string.IsNullOrEmpty(commandLine))
        {
            startInfo.Arguments = commandLine;
        }

        Process process = Process.Start(startInfo);
        return new ProcessEx(process);
    }

    public byte[] ReadMemory(IntPtr address, int size)
    {
        return implementation.ReadMemory(address, size);
    }

    public int WriteMemory(IntPtr address, byte[] data)
    {
        return implementation.WriteMemory(address, data);
    }

    public IEnumerable<ProcessModuleEx> GetModules()
    {
        return implementation.GetModules();
    }

    public void Suspend()
    {
        implementation.Suspend();
    }

    public void Resume()
    {
        implementation.Resume();
    }

    public void Terminate()
    {
        implementation.Terminate();
    }

    public void Dispose()
    {
        implementation.Dispose();
    }
    
    public static ProcessEx GetFirstProcess(string procName, Func<ProcessEx, bool> predicate = null)
    {
        ProcessEx found = null;
        foreach (Process proc in Process.GetProcessesByName(procName))
        {
            // Already found, dispose all other resources to processes.
            if (found != null)
            {
                proc.Dispose();
                continue;
            }

            ProcessEx procEx = new(proc);
            if (predicate != null && !predicate(procEx))
            {
                procEx.Dispose();
                continue;
            }

            found = procEx;
        }

        return found;
    }
}

public abstract class ProcessExBase : IDisposable
{
    public abstract int Id { get; }
    public abstract string Name { get; }
    public abstract IntPtr Handle { get; }
    public abstract ProcessModuleEx MainModule { get; }
    public abstract string MainModuleFileName { get; }
    public abstract IntPtr MainWindowHandle { get; }
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
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
        return geteuid() == 0;
    }

    public virtual void Dispose()
    {
    }

    [DllImport("libc")]
    private static extern uint geteuid();
}

public class ProcessModuleEx
{
    public IntPtr BaseAddress { get; set; }
    public string ModuleName { get; set; }
    public string FileName { get; set; }
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
            return new LinuxProcessEx(pid);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacProcessEx(pid);
        }
        throw new PlatformNotSupportedException();
    }
}

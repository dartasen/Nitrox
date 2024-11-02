using System;
using System.Collections.Generic;
using System.Diagnostics;

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

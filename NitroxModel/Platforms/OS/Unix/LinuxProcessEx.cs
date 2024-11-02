using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NitroxModel.Platforms.OS.Shared;

namespace NitroxModel.Platforms.OS.Unix;

public sealed class LinuxProcessEx : ProcessExBase
{
    public override int Id { get; }

    public override IntPtr Handle => IntPtr.Zero; // Linux doesn't use handles

    public override string Name
    {
        get
        {
            try
            {
                string status = File.ReadAllText($"/proc/{Id}/status");
                string[] lines = status.Split('\n');
                return lines.FirstOrDefault(l => l.StartsWith("Name:"))?.Substring(5).Trim();
            }
            catch (UnauthorizedAccessException)
            {
                // If we can't read the status file, try to get the name from the command line
                try
                {
                    string cmdline = File.ReadAllText($"/proc/{Id}/cmdline");
                    return Path.GetFileName(cmdline.Split('\0')[0]);
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public override ProcessModuleEx MainModule
    {
        get
        {
            // This is a simplified implementation. You might need to parse /proc/{pid}/maps
            // to get more accurate information about the main module.
            return new ProcessModuleEx
            {
                BaseAddress = IntPtr.Zero,
                ModuleName = Name,
                FileName = MainModuleFileName,
                ModuleMemorySize = 0
            };
        }
    }

    public override string MainModuleFileName
    {
        get
        {
            try
            {
                return ReadSymbolicLink($"/proc/{Id}/exe");
            }
            catch (UnauthorizedAccessException)
            {
                // If we don't have permission to read the symlink, return null
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public override IntPtr MainWindowHandle
    {
        get
        {
            // Linux doesn't have a direct equivalent to Windows' MainWindowHandle.
            // This is a placeholder implementation.
            throw new PlatformNotSupportedException("MainWindowHandle is not supported on Linux.");
        }
    }

    public LinuxProcessEx(int pid)
    {
        Id = pid;
        if (!File.Exists($"/proc/{Id}/status"))
        {
            throw new ArgumentException("Process does not exist.", nameof(pid));
        }
    }

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        try
        {
            using FileStream fs = new FileStream($"/proc/{Id}/mem", FileMode.Open, FileAccess.Read);
            fs.Seek((long)address, SeekOrigin.Begin);
            if (fs.Read(buffer, 0, size) != size)
            {
                throw new IOException("Failed to read the specified amount of memory.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read process memory.", ex);
        }
        return buffer;
    }

    public override int WriteMemory(IntPtr address, byte[] data)
    {
        int result = ptrace(PtraceRequest.PTRACE_ATTACH, Id, IntPtr.Zero, IntPtr.Zero);
        if (result < 0)
        {
            throw new InvalidOperationException("Failed to attach to the process.");
        }

        try
        {
            for (int i = 0; i < data.Length; i += sizeof(long))
            {
                long value = BitConverter.ToInt64(data, i);
                if (ptrace(PtraceRequest.PTRACE_POKEDATA, Id, address + i, (IntPtr)value) < 0)
                {
                    throw new InvalidOperationException("Failed to write memory.");
                }
            }
        }
        finally
        {
            ptrace(PtraceRequest.PTRACE_DETACH, Id, IntPtr.Zero, IntPtr.Zero);
        }
        return data.Length;
    }

    public override IEnumerable<ProcessModuleEx> GetModules()
    {
        List<ProcessModuleEx> modules = [];
        string[] lines = File.ReadAllLines($"/proc/{Id}/maps");
        foreach (string line in lines)
        {
            string[] parts = line.Split(' ');
            if (parts.Length < 6)
            {
                continue;
            }
            
            string[] addresses = parts[0].Split('-');
            modules.Add(new ProcessModuleEx
            {
                BaseAddress = (IntPtr)long.Parse(addresses[0], NumberStyles.HexNumber),
                ModuleName = parts[5],
                FileName = parts[5],
                ModuleMemorySize = (int)(long.Parse(addresses[1], NumberStyles.HexNumber) - long.Parse(addresses[0], NumberStyles.HexNumber))
            });
        }
        return modules;
    }

    public override void Suspend()
    {
        if (kill(Id, 19) != 0) // SIGSTOP
        {
            throw new InvalidOperationException("Failed to suspend the process.");
        }
    }

    public override void Resume()
    {
        if (kill(Id, 18) != 0) // SIGCONT
        {
            throw new InvalidOperationException("Failed to resume the process.");
        }
    }

    public override void Terminate()
    {
        if (kill(Id, 9) != 0) // SIGKILL
        {
            throw new InvalidOperationException("Failed to terminate the process.");
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int ptrace(PtraceRequest request, int pid, IntPtr addr, IntPtr data);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    [DllImport("libc", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int readlink(string path, byte[] buf, int bufsiz);

    private static string ReadSymbolicLink(string path)
    {
        const int BUFFER_SIZE = 1024;
        byte[] buffer = new byte[BUFFER_SIZE];
        int bytesRead = readlink(path, buffer, BUFFER_SIZE);
        if (bytesRead < 0)
        {
            throw new IOException("Failed to read symbolic link.");
        }
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }
}

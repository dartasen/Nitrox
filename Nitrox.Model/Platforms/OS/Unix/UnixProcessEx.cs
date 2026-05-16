using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.OS.Windows.Internal;

namespace Nitrox.Model.Platforms.OS.Unix;

#if NET
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
#endif
public sealed partial class UnixProcessEx : ProcessExBase
{
    private readonly int pid;

    public override int Id => pid;
    public override IntPtr Handle => IntPtr.Zero; // Linux doesn't use handles

    public override string? Name
    {
        get
        {
            try
            {
                string status = File.ReadAllText($"/proc/{pid}/status");
                string[] lines = status.Split('\n');
                return lines.FirstOrDefault(l => l.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))?.Substring("Name:".Length).Trim();
            }
            catch (UnauthorizedAccessException)
            {
                // If we can't read the status file, try to get the name from the command line
                try
                {
                    string cmdline = File.ReadAllText($"/proc/{pid}/cmdline");
                    return Path.GetFileName(cmdline.Split('\0')[0]);
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public override bool IsRunning
    {
        get
        {
            if (!base.IsRunning)
            {
                return false;
            }
            try
            {
                string[] lines = File.ReadAllLines($"/proc/{pid}/status");
                string procState = lines.FirstOrDefault(l => l.StartsWith("State:", StringComparison.OrdinalIgnoreCase))?.Substring("State:".Length).Trim();
                return procState?.FirstOrDefault() switch
                {
                    'Z' => false, // Zombie process
                    _ => true
                };
            }
            catch
            {
                // ignored
            }
            return false;
        }
    }

    public override ProcessModuleEx MainModule =>
        // This is a simplified implementation. You might need to parse /proc/{pid}/maps
        // to get more accurate information about the main module.
        new()
        {
            BaseAddress = IntPtr.Zero,
            ModuleName = Name,
            FileName = MainModuleFileName,
            ModuleMemorySize = 0
        };

    public override string? MainModuleFileName
    {
        get
        {
            try
            {
                return ReadSymbolicLink($"/proc/{pid}/exe");
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

    public UnixProcessEx(int pid) : base(pid)
    {
        this.pid = pid;
        if (!File.Exists($"/proc/{this.pid}/status"))
        {
            throw new ArgumentException("Process does not exist.", nameof(pid));
        }
    }

    public UnixProcessEx(Process process) : base(process)
    {
        pid = process.Id;
        if (!File.Exists($"/proc/{pid}/status"))
        {
            throw new ArgumentException("Process does not exist.", nameof(pid));
        }
    }

    public new static bool IsElevated() => geteuid() == 0;

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        try
        {
            using FileStream fs = new($"/proc/{pid}/mem", FileMode.Open, FileAccess.Read);
            fs.Seek(address.ToInt64(), SeekOrigin.Begin);
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
        int result = ptrace(PtraceRequest.PTRACE_ATTACH, pid, IntPtr.Zero, IntPtr.Zero);
        if (result < 0)
        {
            throw new InvalidOperationException("Failed to attach to the process.");
        }

        try
        {
            for (int i = 0; i < data.Length; i += sizeof(long))
            {
                long value = BitConverter.ToInt64(data, i);
                if (ptrace(PtraceRequest.PTRACE_POKEDATA, pid, address + i, (IntPtr)value) < 0)
                {
                    throw new InvalidOperationException("Failed to write memory.");
                }
            }
        }
        finally
        {
            ptrace(PtraceRequest.PTRACE_DETACH, pid, IntPtr.Zero, IntPtr.Zero);
        }
        return data.Length;
    }

    public override IEnumerable<ProcessModuleEx> GetModules()
    {
        List<ProcessModuleEx> modules = [];
        string[] lines = File.ReadAllLines($"/proc/{pid}/maps");
        foreach (string line in lines)
        {
            string[] parts = line.Split(' ');
            if (parts.Length >= 6)
            {
                string[] addresses = parts[0].Split('-');
                modules.Add(new ProcessModuleEx
                {
                    BaseAddress = (IntPtr)long.Parse(addresses[0], NumberStyles.HexNumber),
                    ModuleName = parts[5],
                    FileName = parts[5],
                    ModuleMemorySize = (int)(long.Parse(addresses[1], NumberStyles.HexNumber) - long.Parse(addresses[0], NumberStyles.HexNumber))
                });
            }
        }
        return modules;
    }

    public override void Suspend()
    {
        if (kill(pid, 19) != 0) // SIGSTOP
        {
            throw new InvalidOperationException("Failed to suspend the process.");
        }
    }

    public override void Resume()
    {
        if (kill(pid, 18) != 0) // SIGCONT
        {
            throw new InvalidOperationException("Failed to resume the process.");
        }
    }

    public override void Terminate()
    {
        if (kill(pid, 9) != 0) // SIGKILL
        {
            throw new InvalidOperationException("Failed to terminate the process.");
        }
    }

    /// <summary>
    /// Returns the effective user ID of the current process.
    /// </summary>
    /// <remarks>https://man7.org/linux/man-pages/man2/geteuid.2.html</remarks>
#if NET
    [LibraryImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static partial uint geteuid();
#else
    [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static extern uint geteuid();
#endif

    /// <summary>
    /// Performs tracing operations against another process, such as attach and memory access.
    /// </summary>
    /// <remarks>https://man7.org/linux/man-pages/man2/ptrace.2.html</remarks>
    /// <param name="request">The ptrace operation to perform.</param>
    /// <param name="pid">The process ID of the tracee.</param>
    /// <param name="addr">An operation-specific address argument.</param>
    /// <param name="data">An operation-specific data argument.</param>
#if NET
    [LibraryImport("libc", EntryPoint = "ptrace", SetLastError = true)]
    private static partial int ptrace(PtraceRequest request, int pid, IntPtr addr, IntPtr data);
#else
    [DllImport("libc", EntryPoint = "ptrace", SetLastError = true)]
    private static extern int ptrace(PtraceRequest request, int pid, IntPtr addr, IntPtr data);
#endif

    /// <summary>
    /// Sends a signal to a process.
    /// </summary>
    /// <remarks>https://man7.org/linux/man-pages/man2/kill.2.html</remarks>
    /// <param name="pid">The target process ID.</param>
    /// <param name="sig">The signal number to send.</param>
#if NET
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int kill(int pid, int sig);
#else
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int kill(int pid, int sig);
#endif

    /// <summary>
    /// Reads the target path stored in a symbolic link.
    /// </summary>
    /// <remarks>https://man7.org/linux/man-pages/man2/readlink.2.html</remarks>
    /// <param name="path">The symbolic link path to inspect.</param>
    /// <param name="buf">The buffer that receives the link target bytes.</param>
    /// <param name="bufsiz">The size of <paramref name="buf" /> in bytes.</param>
#if NET
    [LibraryImport("libc", EntryPoint = "readlink", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int readlink(string path, byte[] buf, int bufsiz);
#else
    [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
    private static extern int readlink(string path, byte[] buf, int bufsiz);
#endif

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

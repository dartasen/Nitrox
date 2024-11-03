using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

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
    public string MainWindowTitle => implementation.MainWindowTitle;

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
            foreach ((string key, string value) in environmentVariables)
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
    protected readonly Process Process;
    public virtual int Id => Process?.Id ?? -1;
    public virtual string Name => Process?.ProcessName;
    public virtual IntPtr Handle => Process?.Handle ?? IntPtr.Zero;
    public abstract ProcessModuleEx MainModule { get; }
    public virtual string MainModuleFileName => Process?.MainModule?.FileName;
    public virtual IntPtr MainWindowHandle => Process?.MainWindowHandle ?? IntPtr.Zero;
    public virtual string MainWindowTitle => Process?.MainWindowTitle;
    public abstract byte[] ReadMemory(IntPtr address, int size);
    public abstract int WriteMemory(IntPtr address, byte[] data);
    public abstract IEnumerable<ProcessModuleEx> GetModules();
    public abstract void Suspend();
    public abstract void Resume();
    public abstract void Terminate();

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

public class WindowsProcessEx : ProcessExBase
{
    private bool disposed;
    private IntPtr handle;

    public override IntPtr Handle => handle;

    public override ProcessModuleEx MainModule
    {
        get
        {
            ProcessModule mainModule = Process.MainModule;
            if (mainModule == null)
            {
                return null;
            }
            return new ProcessModuleEx
            {
                BaseAddress = mainModule.BaseAddress,
                ModuleName = mainModule.ModuleName,
                FileName = mainModule.FileName,
                ModuleMemorySize = mainModule.ModuleMemorySize
            };
        }
    }

    public override string MainModuleFileName => Process.MainModule?.FileName;
    public override IntPtr MainWindowHandle => Process.MainWindowHandle;
    public override string MainWindowTitle => Process.MainWindowTitle;

    public WindowsProcessEx(int id) : base(id)
    {
        if (!IsElevated())
        {
            throw new UnauthorizedAccessException("Elevated privileges required.");
        }

        handle = OpenProcess(0x1F0FFF, false, id);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        if (!ReadProcessMemory(handle, address, buffer, size, out int bytesRead) || bytesRead != size)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return buffer;
    }

    public override int WriteMemory(IntPtr address, byte[] data)
    {
        if (!WriteProcessMemory(handle, address, data, data.Length, out int bytesWritten))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return bytesWritten;
    }

    public override IEnumerable<ProcessModuleEx> GetModules()
    {
        return Process.Modules.Cast<ProcessModule>().Select(m => new ProcessModuleEx
        {
            BaseAddress = m.BaseAddress,
            ModuleName = m.ModuleName,
            FileName = m.FileName,
            ModuleMemorySize = m.ModuleMemorySize
        });
    }

    public override void Suspend()
    {
        foreach (ProcessThread thread in Process.Threads)
        {
            IntPtr threadHandle = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
            if (threadHandle != IntPtr.Zero)
            {
                try
                {
                    if (SuspendThread(threadHandle) == uint.MaxValue)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    CloseHandle(threadHandle);
                }
            }
        }
    }

    public override void Resume()
    {
        foreach (ProcessThread thread in Process.Threads)
        {
            IntPtr threadHandle = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
            if (threadHandle != IntPtr.Zero)
            {
                try
                {
                    if (ResumeThread(threadHandle) == -1)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    CloseHandle(threadHandle);
                }
            }
        }
    }

    public override void Terminate()
    {
        Process.Kill();
    }

    public override void Dispose()
    {
        if (!disposed)
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
            Process.Dispose();
            disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResumeThread(IntPtr hThread);
}

public class LinuxProcessEx : ProcessExBase
{
    private readonly int pid;

    public override int Id => pid;
    public override IntPtr Handle => IntPtr.Zero; // Linux doesn't use handles

    public override string Name
    {
        get
        {
            try
            {
                string status = File.ReadAllText($"/proc/{pid}/status");
                string[] lines = status.Split('\n');
                return lines.FirstOrDefault(l => l.StartsWith("Name:"))?.Substring(5).Trim();
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

    public override string MainModuleFileName
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

    public LinuxProcessEx(int pid) : base(pid)
    {
        this.pid = pid;
        if (!File.Exists($"/proc/{this.pid}/status"))
        {
            throw new ArgumentException("Process does not exist.", nameof(pid));
        }
    }

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        try
        {
            using FileStream fs = new($"/proc/{pid}/mem", FileMode.Open, FileAccess.Read);
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

    [DllImport("libc", SetLastError = true)]
    private static extern int ptrace(PtraceRequest request, int pid, IntPtr addr, IntPtr data);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    [DllImport("libc")]
    private static extern int readlink(string path, byte[] buf, int bufsiz);

    private string ReadSymbolicLink(string path)
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

public class MacOSProcessEx : ProcessExBase
{
    private readonly IntPtr task;
    private bool disposed;
    public override int Id { get; }
    public override IntPtr Handle => task;

    public override string Name =>
            // This is a simplified implementation. In a real scenario, you'd use sysctl to get the process name.
            throw new NotImplementedException("Getting process name is not implemented for macOS.");

    public override ProcessModuleEx MainModule =>
            // This is a placeholder implementation. You'll need to use macOS-specific APIs
            // to get accurate information about the main module.
            new()
            {
                BaseAddress = IntPtr.Zero,
                ModuleName = Name,
                FileName = MainModuleFileName,
                ModuleMemorySize = 0
            };

    public MacOSProcessEx(int pid) : base(pid)
    {
        if (!IsElevated())
        {
            throw new UnauthorizedAccessException("Root privileges required.");
        }

        Id = pid;
        if (task_for_pid(mach_task_self(), pid, out task) != 0)
        {
            throw new InvalidOperationException("Failed to get task for pid.");
        }
    }

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (vm_read_overwrite(task, address, (IntPtr)size, handle.AddrOfPinnedObject(), out IntPtr _) != 0)
            {
                throw new InvalidOperationException("Failed to read process memory.");
            }
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }

    public override int WriteMemory(IntPtr address, byte[] data)
    {
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            if (vm_write(task, address, handle.AddrOfPinnedObject(), (IntPtr)data.Length) != 0)
            {
                throw new InvalidOperationException("Failed to write process memory.");
            }
        }
        finally
        {
            handle.Free();
        }
        return data.Length;
    }

    public override IEnumerable<ProcessModuleEx> GetModules()
    {
        // This is a simplified implementation. In a real scenario, you'd use dyld APIs to get the loaded modules.
        throw new NotImplementedException("Getting modules is not implemented for macOS.");
    }

    public override void Suspend()
    {
        if (task_suspend(task) != 0)
        {
            throw new InvalidOperationException("Failed to suspend the process.");
        }
    }

    public override void Resume()
    {
        if (task_resume(task) != 0)
        {
            throw new InvalidOperationException("Failed to resume the process.");
        }
    }

    public override void Terminate()
    {
        if (task_terminate(task) != 0)
        {
            throw new InvalidOperationException("Failed to terminate the process.");
        }
    }

    public override void Dispose()
    {
        if (!disposed)
        {
            // In a real implementation, you'd release the task port here
            disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [DllImport("libSystem.dylib")]
    private static extern int task_for_pid(IntPtr targetTport, int pid, out IntPtr t);

    [DllImport("libSystem.dylib")]
    private static extern IntPtr mach_task_self();

    [DllImport("libSystem.dylib")]
    private static extern int vm_read_overwrite(IntPtr targetTask, IntPtr address, IntPtr size, IntPtr data, out IntPtr outsize);

    [DllImport("libSystem.dylib")]
    private static extern int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, IntPtr size);

    [DllImport("libSystem.dylib")]
    private static extern int task_suspend(IntPtr task);

    [DllImport("libSystem.dylib")]
    private static extern int task_resume(IntPtr task);

    [DllImport("libSystem.dylib")]
    private static extern int task_terminate(IntPtr task);
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
            return new MacOSProcessEx(pid);
        }
        throw new PlatformNotSupportedException();
    }
}

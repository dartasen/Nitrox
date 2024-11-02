using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using NitroxModel.Platforms.OS.Shared;

namespace NitroxModel.Platforms.OS.Windows;

public sealed class WindowsProcessEx : ProcessExBase
{
    private readonly Process process;
    private bool disposed;
    private IntPtr handle;

    public override int Id => process.Id;
    public override string Name => process.ProcessName;
    public override IntPtr Handle => handle;

    public override ProcessModuleEx MainModule
    {
        get
        {
            ProcessModule mainModule = process.MainModule;
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

    public override string MainModuleFileName => process.MainModule?.FileName;
    public override IntPtr MainWindowHandle => process.MainWindowHandle;

    public WindowsProcessEx(int id)
    {
        if (!IsElevated())
        {
            throw new UnauthorizedAccessException("Elevated privileges required.");
        }

        process = Process.GetProcessById(id);
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
        return process.Modules.Cast<ProcessModule>().Select(m => new ProcessModuleEx
        {
            BaseAddress = m.BaseAddress,
            ModuleName = m.ModuleName,
            FileName = m.FileName,
            ModuleMemorySize = m.ModuleMemorySize
        });
    }

    public override void Suspend()
    {
        foreach (ProcessThread thread in process.Threads)
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
        foreach (ProcessThread thread in process.Threads)
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
        process.Kill();
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
            process.Dispose();
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

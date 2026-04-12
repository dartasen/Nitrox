using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.OS.Windows.Internal;

namespace Nitrox.Model.Platforms.OS.Windows;

#if NET
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public sealed class WindowsProcessEx : ProcessExBase
{
    public override ProcessModuleEx? MainModule
    {
        get
        {
            ProcessModule mainModule = Process?.MainModule;
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

    public override string? MainModuleFileName => Process?.MainModule?.FileName;
    public override IntPtr MainWindowHandle => Process?.MainWindowHandle ?? IntPtr.Zero;
    public override string? MainWindowTitle => Process?.MainWindowTitle;

    public override bool IsRunning
    {
        get
        {
            if (!base.IsRunning)
            {
                return false;
            }
            if (Id < 1)
            {
                return false;
            }
            Process? proc = null;
            try
            {
                proc = Process.GetProcessById(Id);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                proc?.Dispose();
            }
        }
    }

    public WindowsProcessEx(int id) : base(id)
    {
    }

    public WindowsProcessEx(Process process) : base(process.Id)
    {
    }

    public new static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();

            WindowsPrincipal principal = new(identity);
            // If process has explicit admin privileges
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return true;
            }

            // Otherwise check if user is in admin group (https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/understand-security-identifiers)
            string admininistratorSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;
            return principal.Claims.Any(claim => claim.Value == admininistratorSid);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        if (Handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException("Process handle is not valid.");
        }

        byte[] buffer = new byte[size];
        if (!ReadProcessMemory(Handle, address, buffer, size, out int bytesRead) || bytesRead != size)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return buffer;
    }

    public override int WriteMemory(IntPtr address, byte[] data)
    {
        if (Handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException("Process handle is not valid.");
        }

        if (!WriteProcessMemory(Handle, address, data, data.Length, out int bytesWritten))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return bytesWritten;
    }

    public override IEnumerable<ProcessModuleEx> GetModules()
    {
        return Process?.Modules.Cast<ProcessModule>().Select(m => new ProcessModuleEx
        {
            BaseAddress = m.BaseAddress,
            ModuleName = m.ModuleName,
            FileName = m.FileName,
            ModuleMemorySize = m.ModuleMemorySize
        }) ?? [];
    }

    public override void Suspend()
    {
        foreach (ProcessThread thread in Process?.Threads ?? new ProcessThreadCollection([]))
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
        foreach (ProcessThread thread in Process?.Threads ?? new ProcessThreadCollection([]))
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

    public override void Terminate() => Process?.Kill();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResumeThread(IntPtr hThread);
}

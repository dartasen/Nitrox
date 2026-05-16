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
public sealed partial class WindowsProcessEx : ProcessExBase
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

    /// <summary>
    /// Closes an open native object handle.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle</remarks>
    /// <param name="hObject">The native handle to close.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);
#else
    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
#endif

    /// <summary>
    /// Reads a block of memory from another process into a caller-provided buffer.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory</remarks>
    /// <param name="hProcess">A handle to the process being read.</param>
    /// <param name="lpBaseAddress">The address in the target process to read from.</param>
    /// <param name="lpBuffer">The destination buffer that receives the copied bytes.</param>
    /// <param name="dwSize">The number of bytes to read.</param>
    /// <param name="lpNumberOfBytesRead">Receives the number of bytes actually read.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "ReadProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);
#else
    [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);
#endif

    /// <summary>
    /// Writes a block of memory into another process at the specified address.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory</remarks>
    /// <param name="hProcess">A handle to the process being written to.</param>
    /// <param name="lpBaseAddress">The address in the target process to write to.</param>
    /// <param name="lpBuffer">The source buffer containing the bytes to write.</param>
    /// <param name="nSize">The number of bytes to write.</param>
    /// <param name="lpNumberOfBytesWritten">Receives the number of bytes actually written.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "WriteProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);
#else
    [DllImport("kernel32.dll", EntryPoint = "WriteProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);
#endif

    /// <summary>
    /// Opens a thread handle with the requested access rights.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openthread</remarks>
    /// <param name="dwDesiredAccess">The requested access rights for the thread handle.</param>
    /// <param name="bInheritHandle">Whether child processes can inherit the returned handle.</param>
    /// <param name="dwThreadId">The operating system thread ID to open.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "OpenThread", SetLastError = true)]
    private static partial IntPtr OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);
#else
    [DllImport("kernel32.dll", EntryPoint = "OpenThread", SetLastError = true)]
    private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
#endif

    /// <summary>
    /// Increments a thread's suspend count to stop it from running.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-suspendthread</remarks>
    /// <param name="hThread">A handle to the thread to suspend.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "SuspendThread", SetLastError = true)]
    private static partial uint SuspendThread(IntPtr hThread);
#else
    [DllImport("kernel32.dll", EntryPoint = "SuspendThread", SetLastError = true)]
    private static extern uint SuspendThread(IntPtr hThread);
#endif

    /// <summary>
    /// Decrements a thread's suspend count and resumes it when the count reaches zero.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-resumethread</remarks>
    /// <param name="hThread">A handle to the thread to resume.</param>
#if NET
    [LibraryImport("kernel32.dll", EntryPoint = "ResumeThread", SetLastError = true)]
    private static partial int ResumeThread(IntPtr hThread);
#else
    [DllImport("kernel32.dll", EntryPoint = "ResumeThread", SetLastError = true)]
    private static extern int ResumeThread(IntPtr hThread);
#endif
}

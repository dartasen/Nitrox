using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NitroxModel.Platforms.OS.Shared;

namespace NitroxModel.Platforms.OS.MacOS;

public sealed class MacProcessEx : ProcessExBase
{
    private const int MAX_PATH_LEN = 1024;
    private const int PROC_PID_PATH_INFO_MAXSIZE = 4 * MAX_PATH_LEN;
    
    private readonly IntPtr task;
    private bool disposed;
    public override int Id { get; }
    public override IntPtr Handle => task;
    public override string Name { get; }

    public override ProcessModuleEx MainModule
    {
        get
        {
            // This is a placeholder implementation. You'll need to use macOS-specific APIs to get accurate information about the main module
            return new ProcessModuleEx
            {
                BaseAddress = IntPtr.Zero,
                ModuleName = Name,
                FileName = MainModuleFileName,
                ModuleMemorySize = -1
            };
        }
    }

    public override string MainModuleFileName
    {
        get
        {
            // This is a placeholder implementation. You'll need to use macOS-specific APIs to get the main module file name
            throw new NotImplementedException("Getting main module file name is not implemented for macOS.");
        }
    }

    public override IntPtr MainWindowHandle
    {
        get
        {
            // MacOS doesn't have a direct equivalent to Windows' MainWindowHandle
            return IntPtr.Zero;
        }
    }

    public MacProcessEx(int pid)
    {
        if (!IsElevated())
        {
            throw new UnauthorizedAccessException("Root privileges required.");
        }

        Id = pid;
        Name = Path.GetFileName(proc_pidpath(pid)) ?? string.Empty;
        
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

    private static unsafe string proc_pidpath(int pid)
    {
        if (pid < 0)
        {
            return null;
        }

        try
        {
            byte* pBuffer = stackalloc byte[PROC_PID_PATH_INFO_MAXSIZE];
            int result = proc_pidpath(pid, pBuffer, PROC_PID_PATH_INFO_MAXSIZE * sizeof(byte));
            if (result <= 0)
            {
                throw new InvalidOperationException("Failed to get process pidpath");
            }

            // OS X uses UTF-8. The conversion may not strip off all trailing \0s so remove them here
            return System.Text.Encoding.UTF8.GetString(pBuffer, result);
        }
        catch (Exception)
        {
            // Ignored
        }

        return null;
    }
    
    [DllImport("libSystem.dylib", EntryPoint = "proc_pidpath")]
    private static extern unsafe int proc_pidpath(int pid, byte* buffer, uint bufferSize);
    
    [DllImport("libSystem.dylib", EntryPoint = "task_for_pid", SetLastError = true)]
    private static extern int task_for_pid(IntPtr targetTport, int pid, out IntPtr t);

    [DllImport("libSystem.dylib", EntryPoint = "task_self_trap", SetLastError = true)]
    private static extern IntPtr mach_task_self();

    [DllImport("libSystem.dylib", EntryPoint = "vm_read_overwrite", SetLastError = true)]
    private static extern int vm_read_overwrite(IntPtr targetTask, IntPtr address, IntPtr size, IntPtr data, out IntPtr outsize);

    [DllImport("libSystem.dylib", EntryPoint = "vm_write", SetLastError = true)]
    private static extern int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, IntPtr size);

    [DllImport("libSystem.dylib", EntryPoint = "task_suspend", SetLastError = true)]
    private static extern int task_suspend(IntPtr task);

    [DllImport("libSystem.dylib", EntryPoint = "task_resume", SetLastError = true)]
    private static extern int task_resume(IntPtr task);

    [DllImport("libSystem.dylib", EntryPoint = "task_terminate", SetLastError = true)]
    private static extern int task_terminate(IntPtr task);
}

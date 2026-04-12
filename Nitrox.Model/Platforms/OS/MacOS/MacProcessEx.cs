using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Model.Platforms.OS.MacOS;

#if NET
[System.Runtime.Versioning.SupportedOSPlatform("maccatalyst")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
#endif
public sealed class MacProcessEx : ProcessExBase
{
    private bool disposed;
    public override IntPtr Handle => IntPtr.Zero;

    public override ProcessModuleEx MainModule
    {
        get
        {
            // This is a placeholder implementation. You'll need to use macOS-specific APIs
            // to get accurate information about the main module.
            return new()
            {
                BaseAddress = IntPtr.Zero,
                ModuleName = Name,
                FileName = MainModuleFileName,
                ModuleMemorySize = 0
            };
        }
    }

    public MacProcessEx(int pid) : base(pid)
    {
    }

    public MacProcessEx(Process process) : base(process)
    {
    }

    public new static bool IsElevated() => geteuid() == 0;

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (vm_read_overwrite(Handle, address, new IntPtr(size), handle.AddrOfPinnedObject(), out IntPtr _) != 0)
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
            if (vm_write(Handle, address, handle.AddrOfPinnedObject(), new IntPtr(data.Length)) != 0)
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
        if (task_suspend(Handle) != 0)
        {
            throw new InvalidOperationException("Failed to suspend the process.");
        }
    }

    public override void Resume()
    {
        if (task_resume(Handle) != 0)
        {
            throw new InvalidOperationException("Failed to resume the process.");
        }
    }

    public override void Terminate()
    {
        if (task_terminate(Handle) != 0)
        {
            throw new InvalidOperationException("Failed to terminate the process.");
        }
    }

    public override void Dispose()
    {
        if (!disposed)
        {
            // In a real implementation, you'd release the task port here
            base.Dispose();
            disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

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

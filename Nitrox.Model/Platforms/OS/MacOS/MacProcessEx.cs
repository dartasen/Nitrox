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
public sealed partial class MacProcessEx : ProcessExBase
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

    /// <summary>
    /// Returns the effective user ID of the current process.
    /// </summary>
    /// <remarks>https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/getuid.2.html</remarks>
#if NET
    [LibraryImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static partial uint geteuid();
#else
    [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static extern uint geteuid();
#endif

    /// <summary>
    /// Reads memory from the target task into an existing caller-provided buffer.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/vm_map.defs#L246</remarks>
    /// <param name="targetTask">The Mach task port whose memory is being read.</param>
    /// <param name="address">The address in the target task to read from.</param>
    /// <param name="size">The number of bytes to read.</param>
    /// <param name="data">The destination buffer address in the calling process.</param>
    /// <param name="outsize">Receives the number of bytes actually copied.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "vm_read_overwrite")]
    private static partial int vm_read_overwrite(IntPtr targetTask, IntPtr address, IntPtr size, IntPtr data, out IntPtr outsize);
#else
    [DllImport("libSystem.dylib", EntryPoint = "vm_read_overwrite")]
    private static extern int vm_read_overwrite(IntPtr targetTask, IntPtr address, IntPtr size, IntPtr data, out IntPtr outsize);
#endif

    /// <summary>
    /// Writes memory into the target task at the specified address.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/vm_map.defs#L217</remarks>
    /// <param name="targetTask">The Mach task port whose memory is being written.</param>
    /// <param name="address">The address in the target task to write to.</param>
    /// <param name="data">The source buffer address in the calling process.</param>
    /// <param name="size">The number of bytes to write.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "vm_write")]
    private static partial int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, IntPtr size);
#else
    [DllImport("libSystem.dylib", EntryPoint = "vm_write")]
    private static extern int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, IntPtr size);
#endif

    /// <summary>
    /// Increments the target task's suspend count and stops its threads from running.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/task.defs#L193</remarks>
    /// <param name="task">The Mach task port to suspend.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "task_suspend")]
    private static partial int task_suspend(IntPtr task);
#else
    [DllImport("libSystem.dylib", EntryPoint = "task_suspend")]
    private static extern int task_suspend(IntPtr task);
#endif

    /// <summary>
    /// Decrements the target task's suspend count and resumes execution when it reaches zero.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/task.defs#L203</remarks>
    /// <param name="task">The Mach task port to resume.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "task_resume")]
    private static partial int task_resume(IntPtr task);
#else
    [DllImport("libSystem.dylib", EntryPoint = "task_resume")]
    private static extern int task_resume(IntPtr task);
#endif

    /// <summary>
    /// Terminates the target task and releases its resources.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/task.defs#L112</remarks>
    /// <param name="task">The Mach task port to terminate.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "task_terminate")]
    private static partial int task_terminate(IntPtr task);
#else
    [DllImport("libSystem.dylib", EntryPoint = "task_terminate")]
    private static extern int task_terminate(IntPtr task);
#endif
}

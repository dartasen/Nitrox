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
    private IntPtr taskPort;
    private bool ownsTaskPort;

    ~MacProcessEx()
    {
        Dispose(disposing: false);
    }

    public override IntPtr Handle => taskPort;

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
        InitializeTaskPort(pid);
    }

    public MacProcessEx(Process process) : base(process)
    {
        InitializeTaskPort(process.Id);
    }

    public new static bool IsElevated() => geteuid() == 0;

    public override byte[] ReadMemory(IntPtr address, int size)
    {
        EnsureTaskPortAvailable();

        byte[] buffer = new byte[size];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (vm_read_overwrite(taskPort, address, new UIntPtr((uint)size), handle.AddrOfPinnedObject(), out UIntPtr _) != 0)
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
        EnsureTaskPortAvailable();

        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            if (vm_write(taskPort, address, handle.AddrOfPinnedObject(), new UIntPtr((uint)data.Length)) != 0)
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
        EnsureTaskPortAvailable();

        if (task_suspend(taskPort) != 0)
        {
            throw new InvalidOperationException("Failed to suspend the process.");
        }
    }

    public override void Resume()
    {
        EnsureTaskPortAvailable();

        if (task_resume(taskPort) != 0)
        {
            throw new InvalidOperationException("Failed to resume the process.");
        }
    }

    public override void Terminate()
    {
        EnsureTaskPortAvailable();

        if (task_terminate(taskPort) != 0)
        {
            throw new InvalidOperationException("Failed to terminate the process.");
        }
    }

    public override void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (TryReleaseTaskPort() is false && disposing)
        {
            throw new InvalidOperationException("Failed to release the process task port.");
        }

        taskPort = IntPtr.Zero;
        ownsTaskPort = false;

        if (disposing)
        {
            base.Dispose();
        }

        disposed = true;
    }

    private void EnsureTaskPortAvailable()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(MacProcessEx));
        }

        if (taskPort == IntPtr.Zero)
        {
            throw new InvalidOperationException("Process task port is not available.");
        }
    }

    private void InitializeTaskPort(int pid)
    {
        if (Process == null || pid < 1)
        {
            return;
        }

        IntPtr selfTask = task_self_trap();
        if (selfTask == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to acquire the current task port.");
        }

        if (pid == Environment.ProcessId)
        {
            taskPort = selfTask;
            ownsTaskPort = false;
            return;
        }

        int result = task_for_pid(selfTask, pid, out IntPtr targetTask);
        if (result != 0)
        {
            throw new UnauthorizedAccessException($"Failed to acquire a task port for process {pid}. macOS may require additional privileges, disabled SIP, or the com.apple.security.cs.debugger entitlement. kern_return_t={result}.");
        }

        taskPort = targetTask;
        ownsTaskPort = true;
    }

    private bool TryReleaseTaskPort()
    {
        if (!ownsTaskPort || taskPort == IntPtr.Zero)
        {
            return true;
        }

        IntPtr selfTask = task_self_trap();
        return selfTask != IntPtr.Zero && mach_port_deallocate(selfTask, taskPort) == 0;
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
    private static partial int vm_read_overwrite(IntPtr targetTask, IntPtr address, UIntPtr size, IntPtr data, out UIntPtr outsize);
#else
    [DllImport("libSystem.dylib", EntryPoint = "vm_read_overwrite")]
    private static extern int vm_read_overwrite(IntPtr targetTask, IntPtr address, UIntPtr size, IntPtr data, out UIntPtr outsize);
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
    private static partial int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, UIntPtr size);
#else
    [DllImport("libSystem.dylib", EntryPoint = "vm_write")]
    private static extern int vm_write(IntPtr targetTask, IntPtr address, IntPtr data, UIntPtr size);
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

    /// <summary>
    /// Looks up the Mach task port for a process ID.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/mach_traps.h#L353</remarks>
    /// <param name="targetTport">The current task port used to authorize the lookup.</param>
    /// <param name="pid">The process ID whose task port should be returned.</param>
    /// <param name="t">Receives the task port for the target process.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "task_for_pid")]
    private static partial int task_for_pid(IntPtr targetTport, int pid, out IntPtr t);
#else
    [DllImport("libSystem.dylib", EntryPoint = "task_for_pid")]
    private static extern int task_for_pid(IntPtr targetTport, int pid, out IntPtr t);
#endif

    /// <summary>
    /// Returns the current process's Mach task port.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/mach_traps.h#L321</remarks>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "task_self_trap")]
    private static partial IntPtr task_self_trap();
#else
    [DllImport("libSystem.dylib", EntryPoint = "task_self_trap")]
    private static extern IntPtr task_self_trap();
#endif

    /// <summary>
    /// Releases a send right for a Mach port name from the current task's IPC space.
    /// </summary>
    /// <remarks>https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/mach_port.defs#L166</remarks>
    /// <param name="task">The current task's IPC space.</param>
    /// <param name="name">The Mach port name to release.</param>
#if NET
    [LibraryImport("libSystem.dylib", EntryPoint = "mach_port_deallocate")]
    private static partial int mach_port_deallocate(IntPtr task, IntPtr name);
#else
    [DllImport("libSystem.dylib", EntryPoint = "mach_port_deallocate")]
    private static extern int mach_port_deallocate(IntPtr task, IntPtr name);
#endif
}

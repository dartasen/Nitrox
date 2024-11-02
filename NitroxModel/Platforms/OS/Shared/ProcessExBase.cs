using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NitroxModel.Platforms.OS.Shared;

public abstract class ProcessExBase : IDisposable
{
    public abstract int Id { get; }
    public abstract string Name { get; }
    public abstract IntPtr Handle { get; }
    public abstract ProcessModuleEx MainModule { get; }
    public abstract string MainModuleFileName { get; }
    public abstract IntPtr MainWindowHandle { get; }
    
    public abstract byte[] ReadMemory(IntPtr address, int size);
    public abstract int WriteMemory(IntPtr address, byte[] data);
    public abstract IEnumerable<ProcessModuleEx> GetModules();
    public abstract void Suspend();
    public abstract void Resume();
    public abstract void Terminate();
    
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

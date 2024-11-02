using System;
using System.Runtime.InteropServices;
using NitroxModel.Platforms.OS.MacOS;
using NitroxModel.Platforms.OS.Unix;
using NitroxModel.Platforms.OS.Windows;

namespace NitroxModel.Platforms.OS.Shared;

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
            return new MacProcessEx(pid);
        }
        
        throw new PlatformNotSupportedException();
    }
}

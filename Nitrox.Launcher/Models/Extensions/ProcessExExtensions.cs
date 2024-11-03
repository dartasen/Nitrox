using System.Diagnostics;
using System.Runtime.InteropServices;
using NitroxModel.Platforms.OS.Shared;
using NitroxModel.Platforms.OS.Windows;

namespace Nitrox.Launcher.Models.Extensions;

public static class ProcessExExtensions
{
    public static void SetForegroundWindowAndRestore(this ProcessEx process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsApi.BringProcessToFront(process.MainWindowHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // xdotool sends an XEvent to X11 window manager on Linux systems. 
            string command = $"xdotool windowactivate $(xdotool search --pid {process.Id} --onlyvisible --desktop '$(xdotool get_desktop)' --name 'nitrox')";
            using Process proc = Process.Start(new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = $"-c \"{command}\"",
            });
        }
    }
}

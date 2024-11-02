using System;

namespace NitroxModel.Platforms.OS.Shared;

public class ProcessModuleEx
{
    public IntPtr BaseAddress { get; set; }
    
    public string ModuleName { get; set; }
    
    public string FileName { get; set; }
    
    public int ModuleMemorySize { get; set; }
}

using System.ComponentModel;

namespace NitroxModel.Discovery;

public enum Platform
{
    [Description("Pirated")]
    PIRATED = -1,

    [Description("Standalone")]
    NONE = 0,

    [Description("Epic Games Store")]
    EPIC = 1,

    [Description("Steam")]
    STEAM = 2,

    [Description("Microsoft")]
    MICROSOFT = 3,

    [Description("Discord")]
    DISCORD = 4,
}

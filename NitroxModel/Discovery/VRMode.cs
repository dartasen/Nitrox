using System.ComponentModel;

namespace NitroxModel.Discovery
{
    public enum VRMode
    {
        [Description("-vrmode none")]
        NONE,

        [Description("-openVR -vrmode SteamVR")]
        STEAMVR,

        [Description("-openVR -vrmode Oculus")]
        OCULUS,
    }
}

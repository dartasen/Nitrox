using System.Reflection;
using NitroxClient.MonoBehaviours;
using NitroxModel.Helper;
using Story;

namespace NitroxPatcher.Patches.Persistent;

/// <summary>
/// Patch to suppress early initialization of story goals, so we can set up our data during initial sync
/// </summary>
public sealed partial class StoryGoalManager_OnSceneObjectsLoaded_Patch : NitroxPatch, IPersistentPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((StoryGoalManager t) => t.OnSceneObjectsLoaded());

    public static bool Prefix() => !Multiplayer.Active;
}

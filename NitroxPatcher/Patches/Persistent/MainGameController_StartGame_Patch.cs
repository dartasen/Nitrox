using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.MonoBehaviours;
using NitroxModel.Helper;

namespace NitroxPatcher.Patches.Persistent;

public partial class MainGameController_StartGame_Patch : NitroxPatch, IPersistentPatch
{
    public static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((MainGameController t) => t.StartGame()));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // waitItem.SetProgress(1f);
        // Multiplayer.SubnauticaLoadingCompleted() [INSERTED LINE]
        // WaitScreen.Remove(waitItem);
        // if (playIntro)
        // {
        //   uGUI.main.intro.Play(new Action(this.OnIntroDone));
        // }
        // yield break;
        return new CodeMatcher(instructions)
               .MatchEndForward(
                   new CodeMatch(OpCodes.Ldarg_0),
                   new CodeMatch(OpCodes.Ldfld),
                   new CodeMatch(OpCodes.Ldc_R4, 1f),
                   new CodeMatch(OpCodes.Callvirt, Reflect.Method((WaitScreen.ManualWaitItem waitItem) => waitItem.SetProgress(default))),
                   new CodeMatch(OpCodes.Ldarg_0),
                   new CodeMatch(OpCodes.Ldfld),
                   new CodeMatch(OpCodes.Call, Reflect.Method(() => WaitScreen.Remove(default))),
                   new CodeMatch(OpCodes.Ldarg_0)
               )
               .MatchStartBackwards(
                   new CodeMatch(OpCodes.Ldarg_0),
                   new CodeMatch(OpCodes.Ldfld),
                   new CodeMatch(OpCodes.Call, Reflect.Method(() => WaitScreen.Remove(default)))
               )
               .ThrowIfInvalid($"Unable to find pattern inside {nameof(MainGameController_StartGame_Patch)}")
               // Insert before last Remove() to avoid FreezeTime to end before multiplayer is ready, see WaitScreen.Update()
               .Insert(
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => Multiplayer.SubnauticaLoadingCompleted()))
               )
               .InstructionEnumeration();
    }
}

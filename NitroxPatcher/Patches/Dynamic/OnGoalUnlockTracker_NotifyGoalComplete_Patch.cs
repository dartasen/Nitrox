﻿using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NitroxClient.Communication.Abstract;
using NitroxModel.Core;
using NitroxModel.Helper;
using NitroxModel.Packets;
using Story;

namespace NitroxPatcher.Patches.Dynamic
{
    public class OnGoalUnlockTracker_NotifyGoalComplete_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly MethodInfo TARGET_METHOD = typeof(OnGoalUnlockTracker).GetMethod("NotifyGoalComplete", BindingFlags.Public | BindingFlags.Instance);

        public static void Prefix(OnGoalUnlockData __instance, string completedGoal)
        {
            Dictionary<string, OnGoalUnlock> goalUnlocks = __instance.ReflectionGet("goalUnlocks", false, false) as Dictionary<string, OnGoalUnlock>;

            if (goalUnlocks.TryGetValue(completedGoal, out _))
            {
                StoryEventSend packet = new StoryEventSend(StoryEventType.GOAL_UNLOCK, completedGoal);
                NitroxServiceLocator.LocateService<IPacketSender>().Send(packet);
            }
        }

        public override void Patch(Harmony harmony)
        {
            PatchPrefix(harmony, TARGET_METHOD);
        }
    }
}

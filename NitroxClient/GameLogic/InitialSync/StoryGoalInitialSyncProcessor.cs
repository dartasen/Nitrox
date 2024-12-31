using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.Packets;
using Story;

namespace NitroxClient.GameLogic.InitialSync;

public class StoryGoalInitialSyncProcessor : InitialSyncProcessor
{
    private readonly TimeManager timeManager;

    public StoryGoalInitialSyncProcessor(TimeManager timeManager)
    {
        this.timeManager = timeManager;

        AddStep(SetTimeData);
        AddStep(SetupStoryGoalManager);
        AddStep(SetScheduledGoals);
        AddStep(SetupAuroraAndSunbeam);
        AddStep(RefreshStoryWithLatestData);
    }

    private static IEnumerator SetupStoryGoalManager(InitialPlayerSync packet)
    {
        Dictionary<string, float> personalGoals = packet.StoryGoalData.PersonalCompletedGoalsWithTimestamp;
        List<string> completedGoals = packet.StoryGoalData.CompletedGoals;
        List<string> radioQueue = packet.StoryGoalData.RadioQueue;

        // Deserialize data for StoryGoalManager
        StoryGoalManager storyGoalManager = StoryGoalManager.main;
        storyGoalManager.version = 3;
        storyGoalManager.completedGoals.AddRange(completedGoals);
        storyGoalManager.pendingRadioMessages.AddRange(radioQueue);

        // Initialize StoryGoalManager (Code copied from StoryGoalManager.OnSceneObjectsLoaded())
        storyGoalManager.compoundGoalTracker.Initialize(storyGoalManager.completedGoals);
        storyGoalManager.onGoalUnlockTracker.Initialize(storyGoalManager.completedGoals);
        storyGoalManager.initialized = true;

        // Start radio
        storyGoalManager.PulsePendingMessages();

        // Initialize CompoundGoalTracker without already completed goals
        CompoundGoalTracker compoundGoalTracker = storyGoalManager.compoundGoalTracker;
        compoundGoalTracker.goals.RemoveAll(goal => completedGoals.Contains(goal.key));

        // Initialize OnGoalUnlockTracker without already completed goals
        OnGoalUnlockTracker onGoalUnlockTracker = storyGoalManager.onGoalUnlockTracker;
        completedGoals.ForEach(goal => onGoalUnlockTracker.goalUnlocks.Remove(goal));

        // Initialize LocationGoalTracker without already completed goals
        LocationGoalTracker locationTracker = storyGoalManager.locationGoalTracker;
        locationTracker.CancelInvoke(nameof(LocationGoalTracker.TrackLocation)); // TrackLocation is being InvokeRepeating inside Start()
        locationTracker.goals.RemoveAll(goal => completedGoals.Contains(goal.key));
        locationTracker.InvokeRepeating(nameof(LocationGoalTracker.TrackLocation), 1f, locationTracker.trackLocationInterval);

        // Initialize BiomeGoalTracker without already completed goals
        BiomeGoalTracker biomeGoalTracker = storyGoalManager.biomeGoalTracker;
        biomeGoalTracker.goals.RemoveAll(goal => completedGoals.Contains(goal.key));

        // Initialize ItemGoalTracker without already completed goals
        ItemGoalTracker itemGoalTracker = storyGoalManager.itemGoalTracker;
        foreach (KeyValuePair<TechType, List<ItemGoal>> entry in storyGoalManager.itemGoalTracker.goals)
        {
            // Goals are all triggered at the same time but we don't know if some entries share certain goals
            if (entry.Value.All(goal => completedGoals.Contains(goal.key)))
            {
                itemGoalTracker.goals.Remove(entry.Key);
            }
        }

        // Restore states of GoalManager and the (tutorial) arrow system
        GoalManager goalManager = GoalManager.main;
        GoalManager.main.completedGoalNames.AddRange(personalGoals.Keys);

        goalManager.CancelInvoke(nameof(GoalManager.UpdateFindGoal));
        foreach (KeyValuePair<string, float> entry in personalGoals)
        {
            Goal entryGoal = GoalManager.main.goals.Find(goal => goal.customGoalName == entry.Key);
            entryGoal?.SetTimeCompleted(entry.Value);
        }

        // Deactivate the current arrow if it was completed
        if (personalGoals.Any(goal => goal.Key == WorldArrowManager.main.currentGoalText))
        {
            WorldArrowManager.main.DeactivateArrow();
        }

        // Restore states of the arrow system (ArrowUpdate() will hide already completed arrows)
        PlayerWorldArrows playerWorldArrows = PlayerWorldArrows.main;
        playerWorldArrows.version = 1;
        playerWorldArrows.completedCustomGoals.AddRange(personalGoals.Keys);

        yield break;
    }

    // Must happen after CompletedGoals
    private static IEnumerator SetupAuroraAndSunbeam(InitialPlayerSync packet)
    {
        TimeData timeData = packet.TimeData;

        AuroraWarnings auroraWarnings = UnityEngine.Object.FindObjectOfType<AuroraWarnings>();
        auroraWarnings.timeSerialized = DayNightCycle.main.timePassedAsFloat;
        auroraWarnings.OnProtoDeserialize(null);

        CrashedShipExploder.main.version = 2;
        StoryManager.UpdateAuroraData(timeData.AuroraEventData);
        CrashedShipExploder.main.timeSerialized = DayNightCycle.main.timePassedAsFloat;
        CrashedShipExploder.main.OnProtoDeserialize(null);

        // Sunbeam countdown is deducted from the scheduled goal PrecursorGunAimCheck
        NitroxScheduledGoal sunbeamCountdownGoal = packet.StoryGoalData.ScheduledGoals.Find(goal => string.Equals(goal.GoalKey, "PrecursorGunAimCheck", StringComparison.OrdinalIgnoreCase));
        if (sunbeamCountdownGoal != null)
        {
            StoryGoalCustomEventHandler.main.countdownActive = true;
            StoryGoalCustomEventHandler.main.countdownStartingTime = sunbeamCountdownGoal.TimeExecute - 2370;
            // See StoryGoalCustomEventHandler.endTime for calculation (endTime - 30 seconds)
        }

        yield break;
    }

    // Must happen after CompletedGoals
    private static IEnumerator SetScheduledGoals(InitialPlayerSync packet)
    {
        List<NitroxScheduledGoal> scheduledGoals = packet.StoryGoalData.ScheduledGoals;

        foreach (NitroxScheduledGoal scheduledGoal in scheduledGoals)
        {
            // Clear duplicated goals that might have appeared during loading and before sync
            StoryGoalScheduler.main.schedule.RemoveAll(goal => goal.goalKey == scheduledGoal.GoalKey);

            ScheduledGoal goal = new()
            {
                goalKey = scheduledGoal.GoalKey,
                goalType = (Story.GoalType)scheduledGoal.GoalType,
                timeExecute = scheduledGoal.TimeExecute,
            };
            if (goal.timeExecute >= DayNightCycle.main.timePassedAsDouble && !StoryGoalManager.main.completedGoals.Contains(goal.goalKey))
            {
                StoryGoalScheduler.main.schedule.Add(goal);
            }
        }

        yield break;
    }

    // Must happen after CompletedGoals
    private static IEnumerator RefreshStoryWithLatestData()
    {
        // If those aren't set up yet, they'll initialize correctly in time
        // Else, we need to force them to acquire the right data
        if (StoryGoalCustomEventHandler.main)
        {
            StoryGoalCustomEventHandler.main.Awake();
        }
        if (PrecursorGunStoryEvents.main)
        {
            PrecursorGunStoryEvents.main.Start();
        }
        yield break;
    }

    private void SetTimeData(InitialPlayerSync packet)
    {
        timeManager.ProcessUpdate(packet.TimeData.TimePacket);
        timeManager.InitRealTimeElapsed(packet.TimeData.TimePacket.RealTimeElapsed, packet.TimeData.TimePacket.UpdateTime, packet.IsFirstPlayer);
        timeManager.AuroraRealExplosionTime = packet.TimeData.AuroraEventData.AuroraRealExplosionTime;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Abstract;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxModel.Packets;

namespace NitroxClient.Communication.Packets.Processors
{
    public class InitialPlayerSyncProcessor : ClientPacketProcessor<InitialPlayerSync>
    {
        private readonly IPacketSender packetSender;
        private readonly HashSet<IInitialSyncProcessor> processors;
        private readonly HashSet<Type> alreadyRan = [];

        private WaitScreen.ManualWaitItem nitroxMainWaitItem;
        private WaitScreen.ManualWaitItem subWaitScreenItem;

        private int cumulativeProcessorsRan;
        private int processorsRanLastCycle;

        public InitialPlayerSyncProcessor(IPacketSender packetSender, IEnumerable<IInitialSyncProcessor> processors)
        {
            this.packetSender = packetSender;
            this.processors = processors.ToSet();
        }

        public override void Process(InitialPlayerSync packet)
        {
            Multiplayer.Main.StartCoroutine(ProcessInitialSyncPacket(packet));
        }

        private IEnumerator ProcessInitialSyncPacket(InitialPlayerSync packet)
        {
            nitroxMainWaitItem = WaitScreen.Add("Nitrox_SyncingWorld");
            nitroxMainWaitItem.SetProgress(0f);

            cumulativeProcessorsRan = 0;
            bool moreProcessorsToRun;
            do
            {
                yield return Multiplayer.Main.StartCoroutine(RunPendingProcessors(packet));

                moreProcessorsToRun = alreadyRan.Count < processors.Count;
                if (moreProcessorsToRun && processorsRanLastCycle == 0)
                {
                    throw new Exception($"Detected circular dependencies in initial packet sync between: {GetRemainingProcessorsText()}");
                }
            } while (moreProcessorsToRun);

            nitroxMainWaitItem.SetProgress(1f);
            WaitScreen.Remove(nitroxMainWaitItem);

            Multiplayer.Main.InitialSyncCompleted = true;

            // When the player finishes loading, we can take back his invincibility
            Player.main.liveMixin.invincible = false;
            Player.main.UnfreezeStats();

            packetSender.Send(new PlayerSyncFinished());
        }

        private IEnumerator RunPendingProcessors(InitialPlayerSync packet)
        {
            processorsRanLastCycle = 0;

            foreach (IInitialSyncProcessor processor in processors)
            {
                if (IsWaitingToRun(processor.GetType()) && HasDependenciesSatisfied(processor))
                {
                    nitroxMainWaitItem.SetProgress(cumulativeProcessorsRan, processors.Count);

                    alreadyRan.Add(processor.GetType());
                    processorsRanLastCycle++;
                    cumulativeProcessorsRan++;

                    Log.Info($"Running {processor.GetType()}");
                    subWaitScreenItem = new WaitScreen.ManualWaitItem(processor.GetType().Name);

                    yield return Multiplayer.Main.StartCoroutine(processor.Process(packet, subWaitScreenItem));
                }
            }
        }

        private bool HasDependenciesSatisfied(IInitialSyncProcessor processor)
        {
            foreach (Type dependentType in processor.DependentProcessors)
            {
                if (IsWaitingToRun(dependentType))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsWaitingToRun(Type processor)
        {
            return alreadyRan.Contains(processor) == false;
        }

        private string GetRemainingProcessorsText()
        {
            string remaining = "";

            foreach (IInitialSyncProcessor processor in processors)
            {
                if (IsWaitingToRun(processor.GetType()))
                {
                    remaining += $" {processor.GetType()}";
                }
            }

            return remaining;
        }
    }
}

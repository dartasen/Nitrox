using System;
using NitroxModel.Packets;
using NitroxModel.DataStructures;

namespace NitroxModel_Subnautica.Packets
{
    [Serializable]
    public class RocketStageUpdate : Packet
    {
        public NitroxId Id { get; }
        public int NewStage { get; set; }
        public TechType CurrentStageTech { get; set;  }

        public RocketStageUpdate(NitroxId id, int newStage, TechType currentStageTech)
        {
            Id = id;
            NewStage = newStage;
            CurrentStageTech = currentStageTech;
        }

        public override string ToString()
        {
            return $"[RocketStageUpdate - Id: {Id}, NewRocketStage: {NewStage}, CurrentStageTech: {CurrentStageTech}]";
        }
    }
}

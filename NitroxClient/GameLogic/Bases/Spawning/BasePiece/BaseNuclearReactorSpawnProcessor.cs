using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.DataStructures;
using NitroxModel.Logger;
using UnityEngine;

namespace NitroxClient.GameLogic.Bases.Spawning.BasePiece
{
    /*
     * When a nuclear reactor is created, two objects are spawned: the main world object (BaseNuclearReactorGeometry) and
     * the core power logic as a separate game object (BaseNuclearReactor, also known as a 'module').  The BaseNuclearReactor
     * resides as a direct child of the base object (probably so UWE could iterate them easy).  When the object spawns, 
     * we use this class to set a deterministic id seeded by the parent id.  This keeps inventory actions in sync.
     */
    public class BaseNuclearReactorSpawnProcessor : BasePieceSpawnProcessor
    {
        protected override TechType[] ApplicableTechTypes { get; } =
        {
            TechType.BaseNuclearReactor
        };

        protected override void SpawnPostProcess(Base latestBase, Int3 latestCell, GameObject finishedPiece)
        {
            NitroxId reactorId = NitroxEntity.GetId(finishedPiece);
            BaseNuclearReactorGeometry nuclearReactor = finishedPiece.RequireComponent<BaseNuclearReactorGeometry>();
            GameObject nuclearReactorModule = nuclearReactor.GetModule().gameObject;

            NitroxId moduleId = reactorId.Increment();
            NitroxEntity.SetNewId(nuclearReactorModule, moduleId);
            Log.InGame($"Applied {moduleId} to module of nuclear reactor {reactorId}");
        }

    }
}

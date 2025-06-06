using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using NitroxModel.Core;
using NitroxModel.DataStructures.Unity;
using NitroxModel.Helper;

namespace NitroxModel.DataStructures.GameLogic;

[Serializable]
[DataContract]
public class AbsoluteEntityCell
{
    [DataMember(Order = 1)]
    public NitroxInt3 BatchId { get; }

    [DataMember(Order = 2)]
    public NitroxInt3 CellId { get; }

    [DataMember(Order = 3)]
    public int Level { get; }

    private static readonly Lazy<IMap> map = new(NitroxServiceLocator.LocateService<IMap>);

    private NitroxInt3 BatchPosition => BatchId * map.Value.BatchSize - map.Value.BatchDimensionCenter;
    public NitroxInt3 Position => BatchPosition + CellId * GetCellSize();

    public NitroxInt3 Center
    {
        get
        {
            NitroxInt3 cellSize = GetCellSize();
            return BatchPosition + CellId * cellSize + (cellSize >> 1);
        }
    }

    [IgnoreConstructor]
    protected AbsoluteEntityCell()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public AbsoluteEntityCell(NitroxInt3 batchId, NitroxInt3 cellId, int level)
    {
        BatchId = batchId;
        CellId = cellId;
        Level = level;
    }

    public AbsoluteEntityCell(NitroxVector3 worldSpace, int level)
    {
        Level = level;

        NitroxVector3 localPosition = (worldSpace + map.Value.BatchDimensionCenter) / map.Value.BatchSize;
        BatchId = NitroxInt3.Floor(localPosition);

        NitroxVector3 cell = (localPosition - BatchId) * GetCellsPerBlock();
        CellId = NitroxInt3.Floor(cell.X + 0.0001f, cell.Y + 0.0001f, cell.Z + 0.0001f);
    }

    public static bool operator ==(AbsoluteEntityCell left, AbsoluteEntityCell right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AbsoluteEntityCell left, AbsoluteEntityCell right)
    {
        return !Equals(left, right);
    }

    public static NitroxInt3 GetCellSize(int level, NitroxInt3 blocksPerBatch)
    {
        // Our own implementation for BatchCells.GetCellSize, that works on the server and client.
        return blocksPerBatch / GetCellsPerBlock(level);
    }

    public static int GetCellsPerBlock(int level)
    {
        switch (level)
        {
            case 0:
                return 10;
            case 1:
            case 2:
            case 3:
                return 5;
            default:
                throw new Exception($"Given level '{level}' does not have any defined cells.");
        }
    }

    public override string ToString()
    {
        return $"[AbsoluteEntityCell Position: {Position} BatchId: {BatchId} CellId: {CellId} Level: {Level} ]";
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }
        if (ReferenceEquals(this, obj))
        {
            return true;
        }
        if (obj.GetType() != GetType())
        {
            return false;
        }
        return Equals((AbsoluteEntityCell)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BatchId != default ? BatchId.GetHashCode() : 0;
            hash = (hash * 397) ^ (CellId != default ? CellId.GetHashCode() : 0);
            hash = (hash * 397) ^ Level;
            return hash;
        }
    }

    public NitroxInt3 GetCellSize()
    {
        return GetCellSize(map.Value.BatchDimensions);
    }

    public NitroxInt3 GetCellSize(NitroxInt3 blocksPerBatch)
    {
        return GetCellSize(Level, blocksPerBatch);
    }

    public int GetCellsPerBlock()
    {
        return GetCellsPerBlock(Level);
    }

    protected bool Equals(AbsoluteEntityCell other)
    {
        return Equals(BatchId, other.BatchId) && Equals(CellId, other.CellId) && Level == other.Level;
    }
}

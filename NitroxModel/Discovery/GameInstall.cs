using System;
using NitroxModel.Helper;

namespace NitroxModel.Discovery;

[Serializable]
public struct GameInstall
{
    public GameInfo GameInfo { get; }

    public Platform Origin { get; }

    public string Path { get; }

    public GameInstall(GameInfo gameInfo, Platform origin, string path)
    {
        Validate.NotNull(gameInfo);
        Validate.IsFalse(string.IsNullOrWhiteSpace(path));
        
        GameInfo = gameInfo;
        Origin = origin;
        Path = path;
    }

    public override string ToString()
    {
        return $"[{nameof(GameInstall)} - GameInfo: {GameInfo?.FullName}, Origin: {Origin}, Path: {Path}";
    }
}

namespace NitroxModel;

public enum GameEnum
{
    SUBNAUTICA = 0,
    SUBNAUTICA_BELOW_ZERO = 1
}

public sealed class GameInfo
{
    public static readonly GameInfo Subnautica = new()
    {
        Game = GameEnum.SUBNAUTICA,
        Name = "Subnautica",
        FullName = "Subnautica",
        ExeName = "Subnautica.exe",
        SteamAppId = 264710,
        MsStoreStartUrl = @"ms-xbl-38616e6e:\\"
    };

    public static readonly GameInfo SubnauticaBelowZero = new()
    {
        Game = GameEnum.SUBNAUTICA_BELOW_ZERO,
        Name = "SubnauticaZero",
        FullName = "Subnautica: Below Zero",
        ExeName = "SubnauticaZero.exe",
        SteamAppId = 848450,
        MsStoreStartUrl = @"ms-xbl-6e27970f:\\"
    };

    public GameEnum Game { get; private set; }
    public string Name { get; private set; }
    public string FullName { get; private set; }
    public string ExeName { get; private set; }
    public int SteamAppId { get; private set; }
    public string MsStoreStartUrl { get; private set; }

    private GameInfo()
    {

    }
}

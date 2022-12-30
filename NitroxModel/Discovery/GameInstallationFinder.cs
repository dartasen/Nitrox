using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NitroxModel.Discovery.Abstract;
using NitroxModel.Discovery.InstallationFinders;

namespace NitroxModel.Discovery;

public class AbstractPlatformGameFinder
{
    private static readonly Lazy<AbstractPlatformGameFinder> instance = new(() => new AbstractPlatformGameFinder());
    public static AbstractPlatformGameFinder Instance => instance.Value;

    private readonly Dictionary<Platform, PlatformGameFinder> gamepathByPlatform = new()
    {
        { Platform.STEAM, new SteamGameRegistryFinder() },
        { Platform.EPIC, new EpicGamesInstallationFinder() },
        { Platform.DISCORD, new DiscordGameFinder() },
    };

    [Obsolete("Old fashionned way")]
    public GameInstall FindGame()
    {

    }

    public string FindGame(GameInfo gameInfo, params Platform[] platform)
    {
        if (gameInfo == null)
        {
            throw new ArgumentNullException(nameof(gameInfo));
        }

        List<GameInstall> path = new();
        //return Path.GetFullPath(path);

        return null;
    }

    public static bool IsGameDirectory(string directory, GameInfo game)
    {
        if (string.IsNullOrWhiteSpace(directory) || game == null)
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "*.exe").Any(file => Path.GetFileName(file)?.Equals(game.ExeName, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}

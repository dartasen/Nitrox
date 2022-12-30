using System;
using System.Collections.Generic;
using System.IO;
using NitroxModel.Discovery.Abstract;

namespace NitroxModel.Discovery.InstallationFinders;

public class DiscordGameFinder : PlatformGameFinder
{
    /// <summary>
    ///     Subnautica Discord is either in appdata or in C:. So for now we just check these 2 paths until we have a better way.
    ///     Discord stores game files in a subfolder called "content" while the parent folder is used to store Discord related files instead.
    /// </summary>
    public override GameInstall? FindGame(GameInfo gameInfo, IList<string> errors = null)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiscordGames", "Subnautica", "content");
        if (HasGameStruct(gameInfo, path, ref errors))
        {
            return new GameInstall(gameInfo, Platform.DISCORD, path);
        }

        path = Path.Combine("C:", "Games", "Subnautica", "content");
        if (HasGameStruct(gameInfo, path, ref errors))
        {
            return new GameInstall(gameInfo, Platform.DISCORD, path);
        }

        return null!;
    }
}

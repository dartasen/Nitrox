using System.Collections.Generic;
using NitroxModel.Discovery.Abstract;
using NitroxModel.Helper;

namespace NitroxModel.Discovery.InstallationFinders;

/// <summary>
///     Tries to read a local config value that contains the installation directory of Subnautica.
/// </summary>
public class ConfigGameFinder : PlatformGameFinder
{
    public override GameInstall? FindGame(GameInfo gameInfo, IList<string> errors = null)
    {
        string path = NitroxUser.PreferredGamePath;
        if (string.IsNullOrEmpty(path))
        {
            errors?.Add($"Configured game path was found empty. Please enter the path to the {gameInfo.FullName} installation.");
            return null!;
        }

        if (!HasGameStruct(gameInfo, path, ref errors))
        {
            return null!;
        }

        return new GameInstall(gameInfo, Platform.NONE, path);
    }
}

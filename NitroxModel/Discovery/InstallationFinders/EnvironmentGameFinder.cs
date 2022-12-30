using System;
using System.Collections.Generic;
using NitroxModel.Discovery.Abstract;

namespace NitroxModel.Discovery.InstallationFinders;

/// <summary>
///     Trying to find the path in environment variables by the key SUBNAUTICA_INSTALLATION_PATH that contains the installation directory of Subnautica.
/// </summary>
public class EnvironmentGameFinder : PlatformGameFinder
{
    public override GameInstall? FindGame(GameInfo gameInfo, IList<string> errors = null)
    {
        string path = Environment.GetEnvironmentVariable("SUBNAUTICA_INSTALLATION_PATH");
        if (string.IsNullOrEmpty(path))
        {
            errors?.Add(@"Configured game path with environment variable SUBNAUTICA_INSTALLATION_PATH was found empty.");
            return null;
        }

        if (!HasGameStruct(gameInfo, path, ref errors))
        {
            return null!;
        }

        return new GameInstall(gameInfo, Platform.NONE, path);
    }
}

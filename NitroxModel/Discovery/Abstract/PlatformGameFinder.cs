using System.Collections.Generic;
using System.IO;

namespace NitroxModel.Discovery.Abstract;

public abstract class PlatformGameFinder
{
    /// <summary>
    ///     Searches for Subnautica installation directory.
    /// </summary>
    /// <param name="errors">Error messages that can be set if it failed to find the game.</param>
    /// <returns>Nullable path to the Subnautica installation path.</returns>
    public abstract GameInstall? FindGame(GameInfo gameInfo, IList<string> errors);

    public static bool HasGameStruct(GameInfo gameInfo, string path, ref IList<string> errors)
    {
        if (!File.Exists(Path.Combine(path, gameInfo.ExeName)))
        {
            errors?.Add($"Configured game path was found without any game executable. Please enter the path to the {gameInfo.FullName} installation.");
            return false;
        }

        if (!Directory.Exists(Path.Combine(path, $"{gameInfo.Name}_Data", "Managed")))
        {
            errors?.Add($@"Game installation directory config '{path}' is invalid. Please enter the path to the {gameInfo.FullName} installation.");
            return false;
        }

        return true;
    }
}

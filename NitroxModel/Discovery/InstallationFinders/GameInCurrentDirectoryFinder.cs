using System.Collections.Generic;
using System.IO;
using NitroxModel.Discovery.Abstract;

namespace NitroxModel.Discovery.InstallationFinders;

public class GameInCurrentDirectoryFinder : PlatformGameFinder
{
    public override GameInstall? FindGame(GameInfo gameInfo, IList<string> errors = null)
    {
        string currentDirectory = Directory.GetCurrentDirectory();

        if (!HasGameStruct(gameInfo, currentDirectory, ref errors))
        {
            return null!;
        }

        return new GameInstall(gameInfo, Platform.NONE, currentDirectory);
    }
}

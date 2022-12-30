using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NitroxModel.Discovery.Abstract;

namespace NitroxModel.Discovery.InstallationFinders;

public class EpicGamesInstallationFinder : PlatformGameFinder
{
    private readonly Regex installLocationRegex = new("\"InstallLocation\"[^\"]*\"(.*)\"", RegexOptions.Compiled);

    public override GameInstall? FindGame(GameInfo gameInfo, IList<string> errors = null)
    {
        // Trying to find the folder where all the game manifests are stored
        string epicGamesManifestsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(epicGamesManifestsDir))
        {
            errors?.Add("Epic games manifest directory does not exist. Verify that Epic Games Store has been installed.");
            return null!;
        }

        // Get all the manifests (JSON format)
        string[] files = Directory.GetFiles(epicGamesManifestsDir, "*.item");
        foreach (string file in files)
        {
            string fileText = File.ReadAllText(file);
            Match match = installLocationRegex.Match(fileText);

            if (!(match.Success || match.Value.Contains("Subnautica")))
            {
                continue;
            }

            return gameInfo.Game switch
            {
                GameEnum.SUBNAUTICA => match.Value.Contains("SubnauticaZero") ? null! : new GameInstall(gameInfo, Platform.EPIC, match.Groups[1].Value),
                GameEnum.SUBNAUTICA_BELOW_ZERO => match.Value.Contains("SubnauticaZero") ? new GameInstall(gameInfo, Platform.EPIC, match.Groups[1].Value) : null!,
                _ => null!
            };
        }

        errors?.Add("Could not find Subnautica installation directory from Epic Games installation records. Verify that Subnautica has been installed with Epic Games Store.");
        return null!;
    }
}

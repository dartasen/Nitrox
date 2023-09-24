using NitroxModel.Discovery.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NitroxModel.Discovery.InstallationFinders;

public class EpicGamesInstallationFinder : IGameFinder
{
    private static readonly Regex installLocationRegex = new("\"InstallLocation\"[^\"]*\"(.*)\"", RegexOptions.Compiled);

    public GameInstallation? FindGame(GameInfo gameInfo, IList<string> errors)
    {
        string commonAppFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string epicGamesManifestsDir = Path.Combine(commonAppFolder, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(epicGamesManifestsDir))
        {
            errors.Add("Epic games manifest directory does not exist. Verify that Epic Games Store has been installed.");
            return null;
        }

        string[] files = Directory.GetFiles(epicGamesManifestsDir, "*.item");
        foreach (string file in files)
        {
            string fileText = File.ReadAllText(file);
            Match match = installLocationRegex.Match(fileText);

            if (match.Success && match.Value.Contains(gameInfo.Name))
            {
                string matchedPath = Path.GetFullPath(match.Groups[1].Value);

                if (!GameInstallationFinder.HasGameExecutable(matchedPath, gameInfo))
                {
                    errors.Add($"Found valid installation directory at '{matchedPath}'. But game's '{gameInfo.ExeName}' is missing.");
                    continue;
                }

                return new()
                {
                    Path = matchedPath,
                    GameInfo = gameInfo,
                    Origin = GameLibraries.EPIC
                };
            }
        }

        errors.Add("Could not find game installation directory from Epic Games installation records. Verify that game has been installed with Epic Games Store.");
        return null;
    }
}

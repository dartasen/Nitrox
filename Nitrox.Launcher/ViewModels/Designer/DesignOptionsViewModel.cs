using Nitrox.Launcher.Models.Design;
using Nitrox.Model.Platforms.Discovery.Models;

namespace Nitrox.Launcher.ViewModels.Designer;

internal class DesignOptionsViewModel : OptionsViewModel
{
    public DesignOptionsViewModel() : base(null!, null!)
    {
        KnownGame selectedGame = new()
        {
            PathToGame = @"C:\Users\Me\Games\Subnautica",
            Platform = Platform.STEAM,
            SelectCommand = SelectDetectedGameCommand,
            IsSelected = true
        };

        SelectedGame = selectedGame;
        KnownInstallations.Add(selectedGame);
        KnownInstallations.Add(new KnownGame
        {
            PathToGame = @"D:\Games\Epic Games\Subnautica",
            Platform = Platform.EPIC,
            SelectCommand = SelectDetectedGameCommand
        });

        LaunchArgs = "-vrmode none";
        ProgramDataFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox";
        ScreenshotsFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\screenshots";
        SavesFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\saves";
        LogsFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\logs";
    }
}

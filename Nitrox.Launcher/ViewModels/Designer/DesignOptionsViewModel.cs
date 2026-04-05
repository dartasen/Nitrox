using Nitrox.Launcher.Models.Design;
using Nitrox.Model.Platforms.Discovery.Models;

namespace Nitrox.Launcher.ViewModels.Designer;

internal class DesignOptionsViewModel : OptionsViewModel
{
    public DesignOptionsViewModel() : base(null!, null!)
    {
        KnownGame selectedGame = CreateKnownGame(@"C:\Steam\steamapps\common\Subnautica", Platform.STEAM, isSelected: true);
        SelectedGame = selectedGame;
        
        KnownInstallations.Add(selectedGame);
        KnownInstallations.Add(CreateKnownGame(@"D:\Games\Epic Games\Subnautica", Platform.EPIC));
        KnownInstallations.Add(CreateKnownGame(@"E:\Heroic\Prefixes\Subnautica", Platform.HEROIC));
        KnownInstallations.Add(CreateKnownGame(@"C:\XboxGames\Subnautica\Content", Platform.MICROSOFT));
        KnownInstallations.Add(CreateKnownGame(@"C:\Users\Me\AppData\Local\DiscordGames\Subnautica", Platform.DISCORD));
        KnownInstallations.Add(CreateKnownGame(@"/Applications/Subnautica.app/Contents", Platform.NONE));

        LaunchArgs = "-vrmode none";
        ProgramDataFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox";
        ScreenshotsFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\screenshots";
        SavesFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\saves";
        LogsFolderDir = @"C:\Users\Me\AppData\Roaming\Nitrox\logs";
    }

    private static KnownGame CreateKnownGame(string path, Platform platform, bool isSelected = false)
    {
        return new KnownGame
        {
            PathToGame = path,
            Platform = platform,
            SelectCommand = null!,
            IsSelected = isSelected
        };
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Nitrox.Model.Platforms.Discovery.Models;
using System.Windows.Input;

namespace Nitrox.Launcher.Models.Design;

public partial class KnownGame : ObservableObject
{
    public required string PathToGame { get; init; }
    public required Platform Platform { get; init; }
    public required ICommand SelectCommand { get; init; }

    [ObservableProperty]
    private bool isSelected;
}

using System;
using Avalonia.Controls.Notifications;
using Nitrox.Launcher.ViewModels;
using NitroxModel.Discovery.Models;
using NitroxModel.Server;

namespace Nitrox.Launcher.Models.Design;

/// <summary>
///     Design-time data for use with the XAML previewer plugin.
/// </summary>
public static class DesignData
{
    static DesignData()
    {
        // Skip initialization if not in design mode.
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            return;
        }

        MainWindowViewModel = new(null, null, null, null, null, null, null, notifications: [new NotificationItem("Something bad happened :(", NotificationType.Error), new NotificationItem("You're in design mode :)")]);
        
        LaunchGameViewModel = new(null, null, null);
        
        ManageServerViewModel = new(null, null) { ServerName = "My fun server" };
        
        CreateServerViewModel = new() { Name = "My Server Name", SelectedGameMode = NitroxGameMode.CREATIVE };
        
        LibraryViewModel = new(null);
        
        CommunityViewModel = new(null);
        
        BlogViewModel = new(null, [new NitroxBlog("Design blog", DateOnly.FromDateTime(DateTime.UtcNow - TimeSpan.FromDays(5)), "google.com", null)]);
        
        UpdatesViewModel = new(null);
        
        OptionsViewModel = new(null)
        {
            SelectedGame = new()
            {
                PathToGame = @"C:\Games\Steam\steamapps\common\Subnautica",
                Platform = Platform.STEAM
            }
        };
        
        DialogBoxViewModel = new() { Title = "Title Text", Description = "Description Text" };
    }
    
    public static MainWindowViewModel MainWindowViewModel { get; }
    public static LaunchGameViewModel LaunchGameViewModel { get; }
    public static ManageServerViewModel ManageServerViewModel { get; }
    public static CreateServerViewModel CreateServerViewModel { get; }
    public static LibraryViewModel LibraryViewModel { get; }
    public static CommunityViewModel CommunityViewModel { get; }
    public static BlogViewModel BlogViewModel { get; }
    public static UpdatesViewModel UpdatesViewModel { get; }
    public static OptionsViewModel OptionsViewModel { get; }
    public static DialogBoxViewModel DialogBoxViewModel { get; }
}
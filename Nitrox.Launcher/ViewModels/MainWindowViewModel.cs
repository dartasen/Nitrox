using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Nitrox.Launcher.Models;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.Models.Utils;
using Nitrox.Launcher.ViewModels.Abstract;
using NitroxModel.Helper;
using NitroxModel.Logger;
using ReactiveUI;

namespace Nitrox.Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BlogViewModel blogViewModel;
    private readonly CommunityViewModel communityViewModel;
    private readonly LaunchGameViewModel launchGameViewModel;
    private readonly OptionsViewModel optionsViewModel;
    private readonly ServersViewModel serversViewModel;
    private readonly UpdatesViewModel updatesViewModel;

    [ObservableProperty]
    private string maximizeButtonIcon = "/Assets/Images/material-design-icons/max.png";

    [ObservableProperty]
    private bool updateAvailableOrUnofficial;

    public ICommand DefaultViewCommand { get; }

    public AvaloniaList<NotificationItem> Notifications { get; init; } = [];

    public RoutingState Router => Screen.Router;
    private IScreen Screen => AppViewLocator.HostScreen;

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(
        ServersViewModel serversViewModel,
        LaunchGameViewModel launchGameViewModel,
        CommunityViewModel communityViewModel,
        BlogViewModel blogViewModel,
        UpdatesViewModel updatesViewModel,
        OptionsViewModel optionsViewModel
    )
    {
        this.launchGameViewModel = launchGameViewModel;
        this.serversViewModel = serversViewModel;
        this.communityViewModel = communityViewModel;
        this.blogViewModel = blogViewModel;
        this.updatesViewModel = updatesViewModel;
        this.optionsViewModel = optionsViewModel;

        DefaultViewCommand = OpenLaunchGameViewCommand;

        this.WhenActivated(disposables =>
        {
            WeakReferenceMessenger.Default.Register<NotificationAddMessage>(this, (_, message) =>
            {
                Notifications.Add(message.Item);
                Task.Run(async () =>
                {
                    await Task.Delay(7000);
                    WeakReferenceMessenger.Default.Send(new NotificationCloseMessage(message.Item));
                });
            });
            WeakReferenceMessenger.Default.Register<NotificationCloseMessage>(this, async (_, message) =>
            {
                message.Item.Dismissed = true;
                await Task.Delay(1000); // Wait for animations
                if (!Design.IsDesignMode) // Prevent design preview crashes
                {
                    Notifications.Remove(message.Item);
                }
            });

            if (!NitroxEnvironment.IsReleaseMode)
            {
                LauncherNotifier.Info("You're now using Nitrox DEV build");
            }

            Task.Run(async () =>
            {
                if (!await NetHelper.HasInternetConnectivityAsync())
                {
                    Log.Warn("Launcher may not be connected to internet");
                    LauncherNotifier.Warning("Launcher may not be connected to internet");
                }
                UpdateAvailableOrUnofficial = await UpdatesViewModel.IsNitroxUpdateAvailableAsync();
            });
            
            Disposable.Create(this, vm =>
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }).DisposeWith(disposables);
        });
    }

    [RelayCommand]
    public void OpenLaunchGameView()
    {
        Screen.Show(launchGameViewModel);
    }

    [RelayCommand]
    public void OpenServersView()
    {
        Screen.Show(serversViewModel);
    }

    [RelayCommand]
    public void OpenCommunityView()
    {
        Screen.Show(communityViewModel);
    }

    [RelayCommand]
    public void OpenBlogView()
    {
        Screen.Show(blogViewModel);
    }

    [RelayCommand]
    public void OpenUpdatesView()
    {
        Screen.Show(updatesViewModel);
    }

    [RelayCommand]
    public void OpenOptionsView()
    {
        Screen.Show(optionsViewModel);
    }

    [RelayCommand]
    public void Minimize()
    {
        MainWindow.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    public void Close()
    {
        MainWindow.Close();
    }

    [RelayCommand]
    public void Maximize()
    {
        if (MainWindow.WindowState == WindowState.Normal)
        {
            MainWindow.WindowState = WindowState.Maximized;
            MaximizeButtonIcon = "/Assets/Images/material-design-icons/restore.png";
        }
        else
        {
            MainWindow.WindowState = WindowState.Normal;
            MaximizeButtonIcon = "/Assets/Images/material-design-icons/max.png";
        }
    }

    [RelayCommand]
    public void Drag(PointerPressedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Source is Visual element && element.GetWindow() is { } window)
        {
            window.BeginMoveDrag(args);
        }
    }
}

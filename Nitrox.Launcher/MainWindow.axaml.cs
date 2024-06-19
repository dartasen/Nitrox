using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HanumanInstitute.MvvmDialogs;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.ViewModels;
using Nitrox.Launcher.Views.Abstract;
using NitroxModel.Platforms.OS.Windows;
using ReactiveUI;
using Serilog;

namespace Nitrox.Launcher;

public partial class MainWindow : WindowBase<MainWindowViewModel>
{
    private readonly IDialogService dialogService;
    private readonly HashSet<Exception> handledExceptions = [];

    public MainWindow(IDialogService dialogService)
    {
        this.dialogService = dialogService;
        // Handle thrown exceptions so they aren't hidden.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                UnhandledExceptionHandler(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (!args.Observed)
            {
                UnhandledExceptionHandler(args.Exception);
            }
        };

        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(UnhandledExceptionHandler);

        this.WhenActivated(d =>
        {
            // Set clicked nav item as selected (and deselect the others).
            Button lastClickedNav = OpenLaunchGameViewButton;
            d(Button.ClickEvent.Raised.Subscribe(args =>
            {
                if (args.Item2 is { Source: Button btn } && btn.Parent?.Classes.Contains("nav") == true && btn.GetValue(NitroxAttached.SelectedProperty) == false)
                {
                    lastClickedNav?.SetValue(NitroxAttached.SelectedProperty, false);
                    lastClickedNav = btn;
                    btn.SetValue(NitroxAttached.SelectedProperty, true);
                }
            }));
            d(PointerPressedEvent.Raised.Subscribe(args =>
            {
                if (args.Item2 is { Handled: false, Source: Control { Tag: string url } control } && control.Classes.Contains("link"))
                {
                    Task.Run(() =>
                    {
                        UriBuilder urlBuilder = new(url)
                        {
                            Scheme = Uri.UriSchemeHttps,
                            Port = -1
                        };
                        Process.Start(new ProcessStartInfo(urlBuilder.Uri.ToString()) { UseShellExecute = true, Verb = "open" })?.Dispose();
                    });
                    args.Item2.Handled = true;
                }
            }));

            try
            {
                ViewModel?.DefaultViewCommand.Execute(null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to execute {nameof(ViewModel.DefaultViewCommand)} command");
            }
        });

        InitializeComponent();

        // Restore default window animations for Windows OS
        if (!Design.IsDesignMode)
        {
            IntPtr? windowHandle = GetTopLevel(this)?.TryGetPlatformHandle()?.Handle;
            if (windowHandle.HasValue)
            {
                WindowsApi.EnableDefaultWindowAnimations(windowHandle.Value);
            }
        }
    }

    private async void UnhandledExceptionHandler(Exception ex)
    {
        if (!handledExceptions.Add(ex))
        {
            return;
        }
        if (Design.IsDesignMode)
        {
            Debug.WriteLine(ex);
            return;
        }

        string title = ex switch
                       {
                           TargetInvocationException e => e.InnerException?.Message,
                           _ => ex.Message
                       } ??
                       ex.Message;

        await dialogService.ShowAsync<DialogBoxViewModel>(model =>
        {
            model.Title = $"Error: {title}";
            model.Description = ex.ToString();
            model.DescriptionForeground = new SolidColorBrush(Colors.Red);
            model.ButtonOptions = ButtonOptions.OkClipboard;
        });

        Environment.Exit(1);
    }

    private void TitleBar_OnPointerPressed(object sender, PointerPressedEventArgs e) => BeginMoveDrag(e);

    private void Window_OnPointerPressed(object sender, PointerPressedEventArgs e) => Focus(); // Allow for de-focusing textboxes when clicking outside of them.
}

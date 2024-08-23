using System.Windows.Input;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ReactiveUI;

namespace Nitrox.Launcher.Models.Design;

public partial class NotificationItem : ObservableObject
{
    public string Message { get; }
    public NotificationType Type { get; }
    public ICommand CloseCommand { get; }

    [ObservableProperty]
    private bool dismissed;

    public NotificationItem(string message, NotificationType type = NotificationType.Information, ICommand closeCommand = null)
    {
        Message = message;
        Type = type;
        CloseCommand = closeCommand ?? ReactiveCommand.Create(() => WeakReferenceMessenger.Default.Send(new NotificationCloseMessage(this)));
    }
}
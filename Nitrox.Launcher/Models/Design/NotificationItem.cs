using System;
using System.Windows.Input;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Nitrox.Launcher.Models.Design;

public partial class NotificationItem : ObservableObject
{
    public string Message { get; }

    public NotificationType Type { get; }

    public DateTimeOffset Timestamp { get; }

    public ICommand CloseCommand { get; }

    [ObservableProperty]
    public partial bool IsDismissed { get; set; }

    [ObservableProperty]
    public partial bool IsRead { get; set; }

    public NotificationItem(string message, NotificationType type = NotificationType.Information, ICommand? closeCommand = null)
    {
        Message = message;
        Type = type;
        Timestamp = DateTimeOffset.Now;
        IsRead = true; // default; becomes unread when the toast expires without dismissal
        CloseCommand = closeCommand ?? new RelayCommand(() => WeakReferenceMessenger.Default.Send(new NotificationCloseMessage(this, UserDismissed: true)));
    }
}

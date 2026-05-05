using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Nitrox.Launcher.Models;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.Models.Extensions;

namespace Nitrox.Launcher.ViewModels;

internal partial class NotificationCenterViewModel : ObservableObject, IMessageReceiver
{
    public AvaloniaList<NotificationItem> History { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    public partial int UnreadCount { get; set; }

    public bool HasUnread => UnreadCount > 0;

    public bool HasHistory => History.Count > 0;

    public NotificationCenterViewModel()
    {
        History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistory));
        this.RegisterMessageListener<NotificationCloseMessage, NotificationCenterViewModel>(static (message, vm) =>
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (vm.History.Contains(message.Item))
                {
                    return; // 7s re-fire after user already dismissed
                }
                if (!message.UserDismissed)
                {
                    message.Item.IsRead = false;
                    vm.UnreadCount++;
                }
                vm.History.Insert(0, message.Item);
            });
        });
    }

    public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(this);

    [RelayCommand]
    public void MarkAllAsRead()
    {
        foreach (NotificationItem item in History)
        {
            item.IsRead = true;
        }
        UnreadCount = 0;
    }

    [RelayCommand]
    public void ClearHistory()
    {
        History.Clear();
        UnreadCount = 0;
    }
}

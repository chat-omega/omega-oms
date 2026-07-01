using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class NotificationsViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public OmsConfig Config;
        private readonly object _syncLock = new();

        public Dispatcher Dispatcher { get; set; }
        public ICollectionView Collection { get; set; }

        [Bindable]
        public partial bool IsVisible { get; set; }

        [Bindable]
        public partial ObservableCollection<NotificationItemViewModel> Notifications { get; set; }

        public NotificationsViewModel()
        {
            Notifications = new ObservableCollection<NotificationItemViewModel>();
            Collection = CollectionViewSource.GetDefaultView(Notifications);
            BindingOperations.EnableCollectionSynchronization(Notifications, _syncLock);
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        internal void AddNotification(Notification notification)
        {
            try
            {
                List<NotificationItemViewModel> oldNotificationsWithSameTag = Notifications.Where(x => x.Notification != null && x.Notification.Tag != null && x.Notification.Tag.GetHashCode() == notification.Tag.GetHashCode()).ToList();
                NotificationItemViewModel notificationModel = new(Config, notification);
                notificationModel.RemoveMeEvent += RemoveNotification;

                lock (_syncLock)
                {
                    if (oldNotificationsWithSameTag.Any())
                    {
                        foreach (NotificationItemViewModel notificationToRemove in oldNotificationsWithSameTag)
                        {
                            Notifications.Remove(notificationToRemove);
                        }
                    }

                    if (Config.NewestNotificationOnTop)
                    {
                        Notifications.Insert(0, notificationModel);
                        while (Notifications.Count > Config.MaxDisplayedNotifications)
                        {
                            Notifications.RemoveAt(Notifications.Count - 1);
                        }
                    }
                    else
                    {
                        Notifications.Add(notificationModel);
                        while (Notifications.Count > Config.MaxDisplayedNotifications)
                        {
                            Notifications.RemoveAt(0);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _log.Error(ex, nameof(AddNotification));
            }
            finally
            {
                UpdateVisibility();
            }
        }

        [Command]
        public void RemoveNotification(NotificationItemViewModel notificationToBeRemoved)
        {
            notificationToBeRemoved.RemoveMeEvent -= RemoveNotification;
            lock (_syncLock)
            {
                if (Notifications.Contains(notificationToBeRemoved))
                {
                    Notifications.Remove(notificationToBeRemoved);
                }
            }
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            IsVisible = Notifications.Count > 0;
        }
    }
}

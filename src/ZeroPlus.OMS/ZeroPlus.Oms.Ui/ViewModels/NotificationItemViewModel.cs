using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System.Timers;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class NotificationItemViewModel : ViewModelBase
    {
        private readonly OmsConfig _config;

        public delegate void RemoveMeEventHandler(NotificationItemViewModel model);
        public event RemoveMeEventHandler RemoveMeEvent;

        [Bindable]
        public partial Notification Notification { get; set; }

        public NotificationItemViewModel(OmsConfig config, Notification notification)
        {
            _config = config;
            Notification = notification;
            Timer timer = new(_config.RemoveNotificationTimerInterval > 0 ? _config.RemoveNotificationTimerInterval : 5000);
            timer.Elapsed += OnTimerElappsed;
            timer.Start();
        }

        private void OnTimerElappsed(object sender, ElapsedEventArgs e)
        {
            RemoveMeEvent?.Invoke(this);
        }

        [Command]
        public void RemoveNotification()
        {
            RemoveMeEvent?.Invoke(this);
        }
    }
}

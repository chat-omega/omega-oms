using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EditHeatmapAlertViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public SpreadHeatmapAlert SpreadHeatmapAlert { get; set; }

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial bool Enabled { get; set; }

        [Bindable]
        public partial double Threshold { get; set; }

        [Bindable]
        public partial bool AudioEnabled { get; set; }

        [Bindable]
        public partial string AudioSound { get; set; }

        [Bindable]
        public partial bool VisualEnabled { get; set; }

        [Bindable]
        public partial bool NotificationEnabled { get; set; }

        [Command]
        public void Save()
        {
            try
            {
                SpreadHeatmapAlert.AlertEnabled = Enabled;
                SpreadHeatmapAlert.Threshold = Threshold;
                SpreadHeatmapAlert.AudioEnabled = AudioEnabled;
                SpreadHeatmapAlert.AudioSound = AudioSound;
                SpreadHeatmapAlert.VisualEnabled = VisualEnabled;
                SpreadHeatmapAlert.NotificationEnabled = NotificationEnabled;

                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Save));
            }
        }

        [Command]
        public void ShareNotificationCommand()
        {
            try
            {
                ShareWithView shareWithView = new();
                ShareWithViewModel shareWithViewModel = (ShareWithViewModel)shareWithView.DataContext;
                shareWithViewModel.SelectedUsers = SpreadHeatmapAlert.ShareWithUsers;
                shareWithViewModel.CanShare = false;
                shareWithView.ShowDialog();
                SpreadHeatmapAlert.ShareWithUsers = shareWithViewModel.SelectedUsers;
            }
            catch (Exception ex)
            {

                _log.Error(ex, nameof(ShareNotificationCommand));
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

        [Command]
        public void TestSound()
        {
            SoundManager.Play(AudioSound);
        }
    }
}

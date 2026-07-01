using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class PositionAlertConfigurationViewModel : ViewModelBase
    {
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public PortfolioManagerModel PortfolioManagerModel { get; }
        public NotificationManager NotificationManager { get; }

        public PositionAlertConfigurationViewModel(PortfolioManagerModel portfolioManagerModel,
                                                   NotificationManager notificationManager)
        {
            PortfolioManagerModel = portfolioManagerModel;
            NotificationManager = notificationManager;
        }

        [Command]
        public void TestSound(string sound)
        {
            SoundManager.Play(sound);
        }
    }
}

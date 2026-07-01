using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class AlertConfigurationViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public Dispatcher Dispatcher { get; set; }
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public PortfolioManagerModel PortfolioManager { get; }

        public AlertConfigurationViewModel(PortfolioManagerModel portfolioManager)
        {
            PortfolioManager = portfolioManager;
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void Save()
        {
            try
            {
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Save));
            }
        }

        [Command]
        public void TestSound(string sound)
        {
            SoundManager.Play(sound);
        }
    }
}

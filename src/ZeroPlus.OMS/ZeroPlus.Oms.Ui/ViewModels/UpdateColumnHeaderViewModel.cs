using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void TitleUpdatedEventHandler(string title);
    public partial class UpdateColumnHeaderViewModel : ViewModelBase
    {
        public event TitleUpdatedEventHandler TitleUpdatedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string Title { get; set; }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(Title))
                {
                    MessageBoxService.ShowMessage($"{nameof(Title)} can not be empty.",
                                                  $"ZeroPlus OMS",
                                                  MessageButton.OK,
                                                  MessageIcon.Warning);
                    return;
                }

                TitleUpdatedEvent?.Invoke(Title);

                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Save));
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

    }
}

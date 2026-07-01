using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum RiskWarningMessageResponse
    {
        Proceed,
        Cancel,
        CancelAll,
    }

    public partial class RiskWarningMessageViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial RiskWarningMessageResponse MessageResult { get; set; }

        [Bindable]
        public partial string Message { get; set; }

        [Bindable]
        public partial string Title { get; set; }

        [Bindable]
        public partial bool ShowCancelAll { get; set; }

        public RiskWarningMessageViewModel()
        {
            MessageResult = RiskWarningMessageResponse.CancelAll;
        }

        [Command]
        public void SendCommand()
        {
            MessageResult = RiskWarningMessageResponse.Proceed;
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelCommand()
        {
            MessageResult = RiskWarningMessageResponse.Cancel;
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelAllCommand()
        {
            MessageResult = RiskWarningMessageResponse.CancelAll;
            CurrentWindowService?.Close();
        }
    }
}

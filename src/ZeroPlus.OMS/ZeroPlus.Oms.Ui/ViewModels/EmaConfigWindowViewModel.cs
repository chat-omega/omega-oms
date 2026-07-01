using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EmaConfigWindowViewModel : ViewModelBase
    {

        [Bindable]
        public partial EmaConfigViewModel EmaConfigViewModel { get; set; }

        [Bindable]
        public partial EmaConfigViewModel Ema2ConfigViewModel { get; set; }

        [Bindable]
        public partial EmaConfigViewModel Ema3ConfigViewModel { get; set; }

        public EmaConfigWindowViewModel(EmaConfigViewModel emaConfigViewModel, EmaConfigViewModel ema2ConfigViewModel, EmaConfigViewModel ema3ConfigViewModel)
        {
            EmaConfigViewModel = emaConfigViewModel;
            Ema2ConfigViewModel = ema2ConfigViewModel;
            Ema3ConfigViewModel = ema3ConfigViewModel;
        }
    }
}

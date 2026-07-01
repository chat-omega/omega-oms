using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class MacdConfigWindowViewModel : ViewModelBase
    {

        [Bindable]
        public partial EmaConfigViewModel FastEmaConfigViewModel { get; set; }
        [Bindable]
        public partial EmaConfigViewModel SlowEmaConfigViewModel { get; set; }
        [Bindable]
        public partial EmaConfigViewModel SignalEmaConfigViewModel { get; set; }

        public MacdConfigWindowViewModel(EmaConfigViewModel fastEmaConfigViewModel,
                                         EmaConfigViewModel slowEmaConfigViewModel,
                                         EmaConfigViewModel signalEmaConfigViewModel)
        {
            FastEmaConfigViewModel = fastEmaConfigViewModel;
            SlowEmaConfigViewModel = slowEmaConfigViewModel;
            SignalEmaConfigViewModel = signalEmaConfigViewModel;
        }
    }
}

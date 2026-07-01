
using DevExpress.Mvvm;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class FishLossConfigViewModel : ViewModelBase
    {

        public FishLossSaveModel Config { get; set; }

        [Bindable]
        public partial bool MarketWidthCheckEnabled { get; set; }

        [Bindable]
        public partial double MinMarketWidth { get; set; }

        [Bindable]
        public partial double MaxMarketWidth { get; set; }

        public FishLossConfigViewModel(FishLossSaveModel config)
        {
            Config = config;
            Load();
        }

        public void Load()
        {
            MarketWidthCheckEnabled = Config.MarketWidthCheckEnabled;
            MinMarketWidth = Config.MinMarketWidth;
            MaxMarketWidth = Config.MaxMarketWidth;
        }

        public FishLossSaveModel GetConfig()
        {
            Config.MarketWidthCheckEnabled = MarketWidthCheckEnabled;
            Config.MinMarketWidth = MinMarketWidth;
            Config.MaxMarketWidth = MaxMarketWidth;
            return Config;
        }
    }
}

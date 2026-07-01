using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Models.Data.EdgeScanner;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class EdgeScanFeedFilterStrategySelectorViewModel : ViewModelBase
    {
        private EdgeScanFeedTradeFilterRowModel _model;

        public EdgeScanFeedTradeFilterRowModel Model { get => _model; set => SetValue(ref _model, value); }

        [Command]
        public void SelectAllStrategiesCommand()
        {
            foreach (StrategyModel item in Model.Strategies)
            {
                item.IsChecked = true;
            }
            Model.UpdateMap();
        }

        [Command]
        public void DeselectAllStrategiesCommand()
        {
            foreach (StrategyModel item in Model.Strategies)
            {
                item.IsChecked = false;
            }
            Model.UpdateMap();
        }
    }
}

using System.Collections.ObjectModel;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public class ModelTradersManagerModel
    {
        public ObservableCollection<ModelTraderViewModel> TraderModels { get; set; }
        public ObservableCollection<PairTraderViewModel> PairTraderModels { get; set; }

        public ModelTradersManagerModel()
        {
            TraderModels = new ObservableCollection<ModelTraderViewModel>();
            PairTraderModels = new ObservableCollection<PairTraderViewModel>();
        }

        internal void AddTrader(ModelTraderViewModel modelTraderViewModel)
        {
            TraderModels.Add(modelTraderViewModel);
        }

        internal void RemoveTrader(ModelTraderViewModel modelTraderViewModel)
        {
            TraderModels.Remove(modelTraderViewModel);
        }

        internal void AddTrader(PairTraderViewModel modelTraderViewModel)
        {
            PairTraderModels.Add(modelTraderViewModel);
        }

        internal void RemoveTrader(PairTraderViewModel modelTraderViewModel)
        {
            PairTraderModels.Remove(modelTraderViewModel);
        }
    }
}

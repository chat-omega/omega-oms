using System;
using System.Collections;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Oms.Ui.Models;
using DevExpress.Data;
using DevExpress.Data.PLinq;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class NewDominatorSpreadQueueViewModel : ViewModelBase
    {
        public ArrayList SelectedRows { get; set; } = new();
        public DominatorTraderModel TraderModel { get; }
        public RealTimeSource Items { get; set; }
        private readonly PLinqServerModeSource _spreads;

        public NewDominatorSpreadQueueViewModel(DominatorTraderModel traderModel)
        {
            TraderModel = traderModel;
            _spreads = new PLinqServerModeSource { Source = TraderModel.DominatorItems };
            Items = new RealTimeSource { DataSource = _spreads };
        }

        [Command]
        public void DisplayFirmTradeActivity()
        {
            throw new NotImplementedException();
        }
    }
}

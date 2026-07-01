using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using ZeroPlus.Models.Data.EdgeScanner;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class EdgeScanFeedFilterBlockedExpirationsConfigViewModel : ViewModelBase
    {
        private EdgeScanFeedTradeFilterRowModel _model;

        public EdgeScanFeedTradeFilterRowModel Model { get => _model; set => SetValue(ref _model, value); }


        [Command]
        public void AddBlockedExpirationCommand()
        {
            if (!Model.BlockedExpirations.Contains(Model.BlockedExpirationInput.Date))
            {
                Model.BlockedExpirations.Add(Model.BlockedExpirationInput.Date);
            }
        }

        [Command]
        public void RemoveBlockedExpirationCommand(DateTime target)
        {
            for (int i = 0; i < Model.BlockedExpirations.Count; i++)
            {
                DateTime item = Model.BlockedExpirations[i];
                if (item.Date == target.Date)
                {
                    Model.BlockedExpirations?.Remove(item);
                }
            }
        }

    }
}

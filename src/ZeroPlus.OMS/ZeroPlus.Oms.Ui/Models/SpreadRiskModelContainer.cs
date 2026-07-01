using System;
using System.Linq;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models
{
    public class SpreadRiskModelContainer : ISpreadRiskModel
    {
        private bool _requested;

        public int Id { get; set; }
        public int TotalOpen { get; set; }
        public int TotalClose { get; set; }
        public bool Action { get; set; }
        public DateTime LastTradeTime { get; set; }
        public string SpreadDescription { get; set; }
        public string Underlying { get; set; }
        public string Tags { get; set; }

        public SpreadRiskModel SpreadRiskModel { get; set; }

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public SpreadRiskModelContainer(string spreadDescription)
        {
            SpreadDescription = spreadDescription;
            SpreadRiskModel = new SpreadRiskModel(spreadDescription);
        }

        public void CopyToModel()
        {
            if (!string.IsNullOrWhiteSpace(Underlying) && SpreadRiskModel.Underlying != Underlying)
            {
                RequestUnderlyingDetails();
            }
            SpreadRiskModel.Id = Id;
            SpreadRiskModel.TotalOpen = TotalOpen;
            SpreadRiskModel.TotalClose = TotalClose;
            SpreadRiskModel.Action = Action;
            SpreadRiskModel.LastTradeTime = LastTradeTime;
            SpreadRiskModel.SpreadDescription = SpreadDescription;
            SpreadRiskModel.Underlying = Underlying;
            SpreadRiskModel.Tags = Tags;
        }

        private async void RequestUnderlyingDetails()
        {
            if (_requested || Action || !OmsCore.Config.ShowEodRiskV2)
            {
                return;
            }

            _requested = true;
            Comms.Models.Data.MarketData.MDUnderlying underDetails = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(Underlying);
            Comms.Models.Data.MarketData.DividendItem dividend = underDetails?.Dividends.OrderBy(x => x.Date).FirstOrDefault();
            if (dividend != null)
            {
                SpreadRiskModel.ExDividend = dividend.Date;
            }
        }
    }
}
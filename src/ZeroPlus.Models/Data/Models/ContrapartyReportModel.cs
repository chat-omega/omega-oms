using System;

namespace ZeroPlus.Models.Data.Models
{
    public record ContraPartyReportModel
    {
        public int FileId { get; set; }
        public string Account { get; set; } = string.Empty;
        public DateTime ExecutionTime { get; set; }
        public string ClOrdID { get; set; } = string.Empty;
        public string OCCID { get; set; } = string.Empty;
        public DateOnly? TradeDate { get; set; }
        public string Side { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Price { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string RQDClOrdID { get; set; } = string.Empty;
        public int? ContraClearingFirm { get; set; }
        public string ContraOpenClose { get; set; } = string.Empty;
        public string ContraAccountType { get; set; } = string.Empty;
        public string MarketMakerSubAccountCode { get; set; } = string.Empty;
        public string TheirExtraText { get; set; } = string.Empty;
        public string TheirClientOrderID { get; set; } = string.Empty;
        public string TheirBrokerID { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string LiquidityIndicator { get; set; } = string.Empty;
    }
}

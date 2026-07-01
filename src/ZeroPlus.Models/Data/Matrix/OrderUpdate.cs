using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix
{
    public class OrderUpdate
    {
        public MessageType MessageType { get; set; }
        public string? ClientGuid { get; set; }
        public ExecType ExecType { get; set; }
        public string? Memo { get; set; }
        public string? Source { get; set; }
        public bool RemoveOnOut { get; set; }
        public string? CancelSubscription { get; set; }
        public Tif Tif { get; set; }
        public Tif TifTake { get; set; }
        public bool DoNotRoute { get; set; }
        public double Discretion { get; set; }
        public uint LegsCount { get; set; }
        public string? UserId { get; set; }
        public string? OrderId { get; set; }
        public string? NewOrigin { get; set; }
        public string? FromSite { get; set; }
        public string? Site { get; set; }
        public bool IsSpreadStrategy { get; set; }
        public string? Country { get; set; }
        public string? IpAddress { get; set; }
        public bool Directed { get; set; }
        public string? Broker { get; set; }
        public string? RejectSource { get; set; }
        public uint RejectCode { get; set; }
        public string? McDest { get; set; }
        public string? BrokerUserId { get; set; }
        public string? BrokerAccount { get; set; }
        public uint ExchId { get; set; }
        public string? Description { get; set; }
        public string? InitOrderId { get; set; }
        public bool InitOrderRecord { get; set; }
        public DateTime TradingDay { get; set; }
        public DateTime TransactionTime { get; set; }
        public uint Mcid { get; set; }
        public uint MscSeq { get; set; }
        public string? Account { get; set; }
        public string? Exchange { get; set; }
        public double FillPrice { get; set; }
        public double FillPriceAlt { get; set; }
        public int FillQty { get; set; }
        public int FillQtyAlt { get; set; }
        public int TotalFilledQty { get; set; }
        public InstrumentType InstType { get; set; }
        public double Price { get; set; }
        public int Qty { get; set; }
        public string? Symbol { get; set; }
        public OrderStatus? OrderStatus { get; set; }
        public Side Side { get; set; }
        public string? Strategy { get; set; }
        public string? Text { get; set; }
        public string? ParentOrderId { get; set; }
        public string? ClearingFirm { get; set; }
        public int ClearingFirmNum { get; set; }
        public string? SentExchangeIdParent { get; set; }
        public string? CancelGuidParent { get; set; }
        public string? CancelGuidStrategy { get; set; }
        public string? OrderIdStrategy { get; set; }
        public string? BrokerExecId { get; set; }
        public string? BrokerExecIdChild { get; set; }
        public string? BrokerOrderId { get; set; }
        public string? BrokerOrderIdChild { get; set; }
        public string? ExClOrdId { get; set; }
        public string? ExClOrdIdOrig { get; set; }
        public double AverageFillPrice { get; set; }
        public double StopPrice { get; set; }
        public int LegRatio { get; set; }
        public int LegNum { get; set; }
        public string? ParentClientId { get; set; }
        public bool IsSpreadStrategyLeg { get; set; }
        public string? ParentClientIdInit { get; set; }
        public Algorithm Algorithm { get; set; }
        public ExecType LegExecType { get; set; }
        public List<SpreadLeg>? Legs { get; set; }
    }
}

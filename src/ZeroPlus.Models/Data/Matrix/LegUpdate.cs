using System;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix;

public class LegUpdate
{
    public MessageType MessageType { get; set; }
    public ExecType ExecType { get; set; }
    public string? OrderId { get; set; }
    public int ExchId { get; set; }
    public bool RemoveOnOut { get; set; }
    public string? NewOrigin { get; set; }
    public string? Text { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public string? UserId { get; set; }
    public string? Description { get; set; }
    public string? InitOrderId { get; set; }
    public bool InitOrderRecord { get; set; }
    public DateTime TradingDay { get; set; }
    public int Mcid { get; set; }
    public DateTime TransactionTime { get; set; }
    public string? IpAddress { get; set; }
    public Tif Tif { get; set; }
    public string? Broker { get; set; }
    public string? RejectSource { get; set; }
    public string? RejectCode { get; set; }
    public string? McDest { get; set; }
    public string? Memo { get; set; }
    public string? Source { get; set; }
    public uint MscSeq { get; set; }
    public string? Country { get; set; }
    public string? Account { get; set; }
    public string? Exchange { get; set; }
    public InstrumentType InstType { get; set; }
    public string? Symbol { get; set; }
    public int Qty { get; set; }
    public string? BrokerUserId { get; set; }
    public string? ParentOrderId { get; set; }
    public string? BrokerAccount { get; set; }
    public Side Side { get; set; }
    public string? ClientGuid { get; set; }
    public double StopPrice { get; set; }
    public int LegRatio { get; set; }
    public int LegNum { get; set; }
    public string? ParentClientId { get; set; }
    public bool IsSpreadStrategyLeg { get; set; }
    public string? ParentClientIdInit { get; set; }
    public int TotalFillQty { get; set; }
    public double AvgFillPrice { get; set; }
    public string? BrokerExecId { get; set; }
    public string? BrokerExecIdChild { get; set; }
    public string? BrokerOrderId { get; set; }
    public string? BrokerOrderIdChild { get; set; }
    public double FillPrice { get; set; }
    public double FillPriceAlt { get; set; }
    public uint FillQty { get; set; }
    public uint FillQtyAlt { get; set; }
    public double Price { get; set; }

}
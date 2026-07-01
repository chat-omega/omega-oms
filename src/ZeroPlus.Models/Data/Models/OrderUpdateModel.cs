using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;

namespace ZeroPlus.Models.Data.Models;

public class OrderUpdateModel
{
    public OrderStatus OrderStatus { get; set; }
    public ExecutionType ExecutionType { get; set; }
    public double Price { get; set; }
    public double AvgPrice { get; set; }
    public double LastPx { get; set; }
    public int LastQty { get; set; }
    public int CumQty { get; set; }
    public int LeavesQty { get; set; }
    public int Qty { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsCancelReject { get; set; }
    public Side? Side { get; set; }
    public string? ClientOrderId { get; set; }
    public string? PrevClientOrderId { get; set; }
    public string? OrigOrderId { get; set; }
    public string? OrderId { get; set; }
    public string? LastExchange { get; set; }
    public string? Message { get; set; }
    public string? Route { get; set; }
    public ContraTrader? ContraTrader { get; set; }

    public override string ToString()
    {
        return $"{nameof(OrderUpdateModel)}. " +
               $"{nameof(OrderStatus)}: {OrderStatus}, " +
               $"{nameof(ExecutionType)}: {ExecutionType}, " +
               $"{nameof(ClientOrderId)}: {ClientOrderId}, " +
               $"{nameof(PrevClientOrderId)}: {PrevClientOrderId}, " +
               $"{nameof(OrigOrderId)}: {OrigOrderId}, " +
               $"{nameof(OrderId)}: {OrderId}, " +
               $"{nameof(LastExchange)}: {LastExchange}, " +
               $"{nameof(Message)}: {Message}, " +
               $"{nameof(Route)}: {Route}, " +
               $"{nameof(Side)}: {Side}, " +
               $"{nameof(Price)}: {Price}, " +
               $"{nameof(AvgPrice)}: {AvgPrice}, " +
               $"{nameof(LastPx)}: {LastPx}, " +
               $"{nameof(LastQty)}: {LastQty}, " +
               $"{nameof(CumQty)}: {CumQty}, " +
               $"{nameof(LeavesQty)}: {LeavesQty}, " +
               $"{nameof(Qty)}: {Qty}, " +
               $"{nameof(LastUpdateTime)}: {LastUpdateTime}, " +
               $"{nameof(IsCancelReject)}: {IsCancelReject}, " +
               $"{nameof(ContraTrader)}: {ContraTrader}";
    }
}
using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;

namespace ZeroPlus.Models.Data.Trading
{
    public class OrderUpdateValues
    {
        public bool ClearOrderIdSet;
        public bool IsCancelEnabled;
        public bool IsModifyEnabled;
        public bool IsSubmitEnabled;
        public bool IsMainOrder;
        public bool IsContraOrder;
        public bool IsHedgeOrder;
        public bool IsLooping;
        public bool AutomationRunning;
        public bool RequiresManualIntervention;
        public int Filled;
        public int CumQuantity;
        public int LastQuantity;
        public int LeavesQuantity;
        public StatusMode StatusMode;
        public DateTime LastUpdateTime;
        public OrderStatus OrderStatus;
        public double LastPrice;
        public double Price;
        public double AveragePrice;
        public double AveragePriceAfterFees;
        public string? Status;
        public string? OrderId;
        public string? Message;
        public string? LocalOrderId;
        public string? OriginalOrderId;
        public string? ParentLocalOrderId;
        public double UnderlyingMidPrice;
        public ContraTrader? ContraTrader;
    }
}

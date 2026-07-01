using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.Data.Trading
{
    public interface IOmsOrderLeg
    {
        string Symbol { get; set; }
        double TotalCommissions { get; }
        Side? Side { get; set; }
        int LastQuantity { get; set; }
        double AveragePrice { get; set; }
        Option Security { get; set; }
        int Ratio { get; set; }
        double Delta { get; set; }
    }

    public interface IOmsOrder
    {
        string SpreadId { get; set; }
        string SpreadType { get; set; }
        string Symbol { get; set; }
        Side? Side { get; set; }
        double Price { get; set; }
        double AveragePrice { get; set; }
        int LastQuantity { get; set; }
        double TotalCommissions { get; }
        double TotalDelta { get; set; }
        DateTime LastUpdateTime { get; set; }
        List<IOmsOrderLeg> TradedLegs { get; }
        List<IOmsOrderLeg> OrderLegs { get; }
        double UnderMid { get; }
        int CumulativeQuantity { get; set; }
        int FilledQty { get; set; }
        double Bid { get; set; }
        double Ask { get; set; }
    }
}

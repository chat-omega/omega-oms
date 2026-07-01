using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public abstract class SmartStrategyData : ISmartStrategyData
{
    public bool ShowInRoutesList { get; set; }
    public uint Type { get; set; }
    public InstrumentType InstrumentType { get; set; }
    public List<string>? Exchanges { get; set; } = new();
    public List<string>? ExchangesTake { get; set; } = new();
    public uint? ReminderQty { get; set; }
    public uint? WorkingQty { get; set; }
    public uint? MinWorkingQty { get; set; }
    public MakeTake? MakeTake { get; set; }
    public double? DiscretionTake { get; set; }
    public uint? MinQuoteQty { get; set; }
    public bool? TakeHidden { get; set; }
    public Algorithm? Algorithm { get; set; }
    public double? UndPrice { get; set; }
    public PriceMethod? PriceMethod { get; set; }
    public double? MaxPriceUnd { get; set; }
    public double? MinPriceUnd { get; set; }
    public double? PriceRange { get; set; }
    public DateTime? LimitToMarketTime { get; set; }
    public bool? AtsMode { get; set; }
    public bool? CancelOnHalt { get; set; }

    public void CopyFrom(SmartStrategyData other)
    {
        InstrumentType = other.InstrumentType;

        Exchanges?.Clear();
        if (other.Exchanges != null)
        {
            foreach (var exch in other.Exchanges)
            {
                Exchanges?.Add(exch);
            }
        }

        ExchangesTake?.Clear();
        if (other.ExchangesTake != null)
        {
            foreach (var exch in other.ExchangesTake)
            {
                ExchangesTake?.Add(exch);
            }
        }

        ReminderQty = other.ReminderQty;
        MinWorkingQty = other.MinWorkingQty;
        MakeTake = other.MakeTake;
        DiscretionTake = other.DiscretionTake;
        MinQuoteQty = other.MinQuoteQty;
        TakeHidden = other.TakeHidden;
        Algorithm = other.Algorithm;
        UndPrice = other.UndPrice;
        PriceMethod = other.PriceMethod;
        MaxPriceUnd = other.MaxPriceUnd;
        MinPriceUnd = other.MinPriceUnd;
        PriceRange = other.PriceRange;
        LimitToMarketTime = other.LimitToMarketTime;
        AtsMode = other.AtsMode;
        CancelOnHalt = other.CancelOnHalt;
        WorkingQty = other.WorkingQty;
    }
}
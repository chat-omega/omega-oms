using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public class SyntheticSpreadStrategyData : SmartStrategyData
{
    public static int ConfigId { get; } = 118;

    /// <summary>
    /// Maximum number of tries (attempts) to send combo orders and/or synthetic legs; default is 1000.
    /// Every set of orders, whether designed to create a new position or to fix an invalid ratio, is considered as a new try.
    /// </summary>
    public uint? NumOfTries { get; set; }

    /// <summary>
    /// Boolean field. If set to true, partial fill price may violate the limit as long as the average fill price does not violate. Defaults to false.
    /// Parent order limit price = 2.5, size = 10.
    /// Strategy is targeting price of 2.5 but got a partial fill with fill qty = 6 and fill price = 2.3
    /// If False or omitted, strategy continues targeting price of 2.5
    /// If True strategy now will be targeting price of 2.8:
    /// Average price = (2.3 * 6 + 2.8 * 4) / 10 = 2.5
    /// The average price does not violate the limit, while the second partial fill may violate the limit.
    /// 0 = False (default)
    /// 1 = True
    /// </summary>
    public bool? SpreadPriceDiscretion { get; set; }

    /// <summary>
    /// Timeout in minutes which the strategy will be cancelled if order is legged with a bad ratio (can be fractional).
    /// Timer resets when good ratio achieved. Defaults to -1 (infinity).
    /// If MK_STRATEGY_DATA_FIELD_BAD_RATIO_PRICE_DISCRETION is defined the discretion will be applied rather that cancelling the order.
    /// </summary>
    public double? BadRatioTimeout { get; set; }

    /// <summary>
    /// A discretion applied to the spread price. If defined, this price discretion will come into play if a bad ratio exceeds the timeout and/or retry threshold.
    /// </summary>
    public double? BadRatioPriceDiscretion { get; set; }

    /// <summary>
    /// The number of retries to fill a spread leg if a bad ratio is detected (before the strategy proceeds to apply discretion).
    /// This field is useless if the MK_STRATEGY_DATA_FIELD_BAD_RATIO_PRICE_DISCRETION is not defined.
    /// </summary>
    public uint? BadRatioTryThreshold { get; set; }

    /// <summary>
    /// 0 = All legs will be sent as separated single orders (standard Synthetic Spread behavior)
    /// 1 = ONLY the equity leg is separated; all other legs are sent as a package.
    /// </summary>
    public bool? SeparateEquityLeg { get; set; }

    /// <summary>
    /// 1 = Indicates that order shall participate in extended trading hours.
    /// 0 = Order participates in regular trading hours only(default value).
    /// </summary>
    public bool? ExtTradingHours { get; set; }

    /// <summary>
    /// Direct handling to synthetic mode only. Will not consider the complex book for parent order.
    /// </summary>
    public bool? LeggingOnly { get; set; }

    /// <summary>
    /// The type of passive leg logic to use.
    /// </summary>
    public Algorithm? SynthPassiveMode { get; set; }

    /// <summary>
    /// Period of time (ms) to wait before cancelling a passive leg if marketable quotes are seen.
    /// </summary>
    public uint? SynthPassiveCancelDelayMs { get; set; }

    /// <summary>
    /// 0 = False (default)
    /// 1 = True
    /// When in balance, limit exchanges to inverted or free exchanges.
    /// </summary>
    public bool? SynthFeeOptimal { get; set; }

    /// <summary>
    /// 0 = False (default)
    /// 1 = True
    /// During complex package steps, do not post the package, only target if quote exists.
    /// </summary>
    public bool? SynthComplexTakeOnly { get; set; }

    /// <summary>
    /// The execution type to be used for each leg, either Limit or Market. 
    /// </summary>
    public ExecType? LegExecType { get; set; } = ExecType.LIMIT;

    /// <summary>
    /// The TIF to be used on each leg: DAY, FOK or IOC. 
    /// </summary>
    public Tif? LegTif { get; set; }

    /// <summary>
    /// The length in seconds to wait on a given exchange before the leg is canceled. 
    /// </summary>
    public uint? LegTimeout { get; set; }

    /// <summary>
    /// A flag that determines whether to hedge the order’s option quantity with stock based on triggering volatility’s delta.
    /// You can customize the order type for the stock portion of this order as well. By default, the Hedge leg is a market order.
    /// </summary>
    public bool? Hedge { get; set; }

    /// <summary>
    /// The amount of underlying shares that a user would like to display at a given exchange. 
    /// </summary>
    public uint? DisplayQty { get; set; }

    /// <summary>
    /// The increment amount of underlying shares that a user chooses to send on hedge orders.
    /// A minimum increment of 100 would prevent odd-lot orders.
    /// </summary>
    public uint? QtyIncluded { get; set; }

    public SyntheticSpreadStrategyData()
    {
        Type = 14;
        InstrumentType = InstrumentType.SPREAD;
    }

    public void CopyFrom(SyntheticSpreadStrategyData other)
    {
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
        NumOfTries = other.NumOfTries;
        SpreadPriceDiscretion = other.SpreadPriceDiscretion;
        BadRatioTimeout = other.BadRatioTimeout;
        BadRatioPriceDiscretion = other.BadRatioPriceDiscretion;
        BadRatioTryThreshold = other.BadRatioTryThreshold;
        WorkingQty = other.WorkingQty;
        SeparateEquityLeg = other.SeparateEquityLeg;
        ExtTradingHours = other.ExtTradingHours;
        LeggingOnly = other.LeggingOnly;
        SynthPassiveMode = other.SynthPassiveMode;
        SynthPassiveCancelDelayMs = other.SynthPassiveCancelDelayMs;
        SynthFeeOptimal = other.SynthFeeOptimal;
        SynthComplexTakeOnly = other.SynthComplexTakeOnly;
        LegExecType = other.LegExecType;
        LegTif = other.LegTif;
        LegTimeout = other.LegTimeout;
        Hedge = other.Hedge;
        DisplayQty = other.DisplayQty;
        QtyIncluded = other.QtyIncluded;
    }
}
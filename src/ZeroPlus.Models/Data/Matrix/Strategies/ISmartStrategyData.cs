using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public interface ISmartStrategyData
{
    public uint Type { get; }

    /// <summary>
    /// The instrument type for this strategy. 
    /// </summary>
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// Sub-blob containing option exchanges to consider as part of this strategy.
    /// If not provided, will use an optimal make exchange fee sorted list specific to your account
    /// </summary>
    public List<string>? Exchanges { get; }

    /// <summary>
    /// Sub-blob containing option exchanges to consider as part of this strategy for targeted orders.
    /// If not provided, will use an optimal take exchange fee sorted list specific to your account.
    /// </summary>
    public List<string>? ExchangesTake { get; }

    /// <summary>
    /// Minimum bid or ask size to leave (or ignore) on an exchange.
    /// If the exchange has a size of 5 and the remainder quantity is 2 then the most the algo will try to take from the exchange is 3.
    /// </summary>
    public uint? ReminderQty { get; set; }

    /// <summary>
    /// Defaults to order quantity, if used conjunction with min working quantity will send random values between the two.
    /// </summary>
    public uint? WorkingQty { get; set; }

    /// <summary>
    /// Minimum working quantity to be present at any given time.  If this value is present, the working quantity will be randomized between the minimum quantity and the maximum quantity.
    /// </summary>
    public uint? MinWorkingQty { get; set; }

    /// <summary>
    /// Take only - Only allow take orders.
    /// Take/Make - Allow both make and take orders
    /// Make only - Sit a tick outside of the bid or offer to attempt to never take.
    /// </summary>
    public MakeTake? MakeTake { get; set; }

    /// <summary>
    /// Specifies the acceptable offset with which to take the liquidity:
    /// Order is to sell at .90 but willing to take at .85 (discretion is -.05)
    /// Must be positive or zero for a buy order.
    /// Must be negative or zero for a sell order.
    /// </summary>
    public double? DiscretionTake { get; set; }

    /// <summary>
    /// Minimum bid/ask size that must be present on an exchange to consider sending a take order.
    /// If the min quote qty is 10 and the exchange has a size of 5, it will not be considered for a take order.
    /// </summary>
    public uint? MinQuoteQty { get; set; }

    /// <summary>
    /// If true (default), the strategy will oversize take orders in the attempt to take hidden liquidity.
    /// If false order sizes will not exceed the market bid/ask size.
    /// </summary>
    public bool? TakeHidden { get; set; }

    /// <summary>
    /// Method to use when routing child orders.
    /// Default – try to execute at the best price and fee.
    /// Aggressive –Capture as much liquidity as possible.May convert IOC take orders to DAY orders.
    /// </summary>
    public Algorithm? Algorithm { get; set; }

    /// <summary>
    /// Required if price method is defined
    /// </summary>
    public double? UndPrice { get; set; }

    /// <summary>
    /// Underlying trigger price method.
    /// </summary>
    public PriceMethod? PriceMethod { get; set; }

    /// <summary>
    /// Maximum underlying price allowed.
    /// The order will only trigger if the currently underlying price is <= the set value.
    /// </summary>
    public double? MaxPriceUnd { get; set; }

    /// <summary>
    /// Minimum underlying price allowed.
    /// The order will only trigger if the currently underlying price is >= the set value.
    /// </summary>
    public double? MinPriceUnd { get; set; }

    /// <summary>
    /// Pegging must be enabled.
    /// No change in orders allocation if market price is worse than the order price +/- this value.
    /// </summary>
    public double? PriceRange { get; set; }

    /// <summary>
    /// YYYYMMDD-HH:MM:SS
    /// UTC time at which a Limit order becomes a Market order.
    /// Only valid on Day orders.
    /// It must be for the current day, and the time must be after the current time.
    /// Not valid for all strategies.See the individual strategies for support.
    /// </summary>
    public DateTime? LimitToMarketTime { get; set; }

    /// <summary>
    /// 0 = Do not participate in QRX
    /// 1 = Participate in QRX(default)
    /// Indicates how the order is to interact with Matrix QRX.
    /// </summary>
    public bool? AtsMode { get; set; }

    /// <summary>
    /// 0 = Do not cancel the strategy (default)
    /// 1 = Cancel the strategy
    /// Indicates the strategy behavior when the underlying is halted.
    /// </summary>
    public bool? CancelOnHalt { get; set; }
}
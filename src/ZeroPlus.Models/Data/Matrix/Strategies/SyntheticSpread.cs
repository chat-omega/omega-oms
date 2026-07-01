using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public class SyntheticSpread : IHaveRisk
{
    /// <summary>
    /// Must be set to MK_MESSAGE_TYPE_NEW.
    /// </summary>
    [Required]
    public MessageType MessageType { get; set; } = MessageType.NEW;

    /// <summary>
    /// The client ID assigned to the order.
    /// This ID will not change for the life of the order.
    /// You can use this ID to cancel or modify orders as well as key off this ID in returned execution messages.
    /// This must be unique for the specified day.
    /// It must be no longer than 40 characters.
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string? ClientGuid { get; set; }

    /// <summary>
    /// The Matrix Executions account number.
    /// </summary>
    [Required]
    public string? Account { get; set; }

    /// <summary>
    /// The instrument type.
    /// </summary>
    [Required]
    public InstrumentType? InstrumentType { get; set; }

    /// <summary>
    /// The Matrix Executions exchange name.
    /// See “Tradable Exchanges” for a list of valid exchanges.
    /// </summary>
    [Required]
    public string? Exchange { get; set; }

    /// <summary>
    /// Execution type.
    /// </summary>
    [Required]
    public ExecType? ExecutionType { get; }

    /// <summary>
    /// The order price.
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// The order quantity.
    /// </summary>
    public int OrderQuantity { get; set; }

    /// <summary>
    /// This field is available for user memo data.
    /// This may be any free form string.
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// The user may populate this string? with free form text.
    /// It can contain any characters, such as “blackbox1” or “traderapp2”.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Optional open/close flag.
    /// If not present, we will set this flag for you based on your current position.
    /// </summary>
    public OpenClose? OpenClose { get; set; }

    /// <summary>
    /// If set to TRUE this will cause the order to be removed from the R3 message window and position server once it goes out.
    /// The order will only disappear if a fill is not present on the order.
    /// </summary>
    public bool? RemoveOnOut { get; set; }

    /// <summary>
    /// Trading strategy.
    /// This is not supported on all exchanges.
    /// </summary>
    public Strategy? Strategy { get; }

    /// <summary>
    /// Display quantity.
    /// </summary>
    public int? DisplayQty { get; set; }

    /// <summary>
    /// If not present, MK_TIF_DAY will be assumed.
    /// </summary>
    public Tif Tif { get; set; } = Tif.DAY;

    /// <summary>
    /// The peg method.
    /// </summary>
    public PegMethod? PegMethod { get; set; }

    /// <summary>
    /// The peg offset.
    /// Required when MK_ORDER_FIELD_PEG_METHOD is present.
    /// </summary>
    public double? PegOffset { get; set; }

    /// <summary>
    /// The discretion.
    /// </summary>
    public double? Discretion { get; set; }

    /// <summary>
    /// Time in force to use for targeting (take) orders
    /// </summary>
    public Tif TifTake { get; set; }

    /// <summary>
    /// 1 = Indicates that order shall participate in extended trading hours.
    /// 0 = Order participates in regular trading hours only(default value).
    /// </summary>
    public bool? ExtTradingHours { get; set; }

    /// <summary>
    /// MK_PEG_METHOD_PEG_DIRECTION_BOTH	= move peg price no matter price better or worse.
    /// MK_PEG_METHOD_PEG_DIRECTION_WORSE only move the pegged price when it worsens the current price
    /// MK_PEG_METHOD_PEG_DIRECTION_BETTER only move the pegged price when it betters the current price
    /// </summary>
    public PegDirection? PegDirection { get; set; }

    public SyntheticSpreadStrategyData StrategyData { get; }

    /// <summary>
    /// Required for spreads.
    /// Add the populated blob to the MK_ORDER_FIELD_LEG_1 though MK_ORDER_FIELD_LEG_4 fields (see 7.3).
    /// </summary>
    [Required]
    public List<SpreadLeg> Legs { get; } = new();

    /// <summary>
    /// Required for spreads. Total number of legs.
    /// </summary>
    [Required]
    public int LegsCount => Legs.Count;

    /// <summary>
    /// For ZP internal use
    /// </summary>
    public int CancelDelay { get; set; }

    /// <summary>
    /// For ZP internal use
    /// </summary>
    public string? Destination { get; set; }
    public uint UserId { get; set; }
    public uint RiskCheckId { get; set; }
    public bool RiskCheckPassed { get; set; }
    public string? RiskCheckMessage { get; set; }

    public SyntheticSpread()
    {
        ExecutionType = ExecType.LIMIT;
        InstrumentType = Enums.Matrix.InstrumentType.STRATEGY;
        Exchange = "RSKY";
        Strategy = Enums.Matrix.Strategy.REDSKY;
        StrategyData = new SyntheticSpreadStrategyData();
    }

}
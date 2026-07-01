using System.ComponentModel.DataAnnotations;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix
{
    public class Order
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
        /// Required for options.
        /// </summary>
        public string? OptionType { get; set; }

        /// <summary>
        /// Required for options.
        /// </summary>
        public string? Strike { get; set; }

        /// <summary>
        /// The Matrix Executions trading symbol.
        /// This is required unless MK_ORDER_FIELD_SYMBOL_ROOT and MK_ORDER_FIELD_EXPIRYMD are present.
        /// The one exception to this rule is when sending orders to the RSKY exchange for strategy orders.
        /// You must populate the symbol field when sending orders to RSKY exchange.
        /// Do not have to be populated for spreads and spread strategies (SynthSprd and SmartSpread).
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// The Matrix Executions symbol root.
        /// This is required unless MK_ORDER_FIELD_SYMBOL is present.
        /// If root is used, the MK_ORDER_FIELD_EXPIRYMD is also required.
        /// </summary>
        public string? SymbolRoot { get; set; }

        /// <summary>
        /// Required if MK_ORDER_FIELD_SYMBOL_ROOT is present.
        /// </summary>
        public string? ExpiryMd { get; set; }

        /// <summary>
        /// The Matrix Executions exchange name.
        /// See “Tradable Exchanges” for a list of valid exchanges.
        /// </summary>
        [Required]
        public string? Exchange { get; set; }

        /// <summary>
        /// Side. Do not have to be populated for spreads and spread strategies (SynthSprd and SmartSpread).
        /// </summary>
        [Required]
        public string? Side { get; set; }

        /// <summary>
        /// Optional side description.
        /// If not present, we will set this flag for you based on your current position.
        /// </summary>
        public string? SideType { get; set; }

        /// <summary>
        /// Execution type.
        /// </summary>
        [Required]
        public ExecType? ExecutionType { get; set; }

        /// <summary>
        /// The order price.
        /// </summary>
        public double? Price { get; set; }

        /// <summary>
        /// The order quantity.
        /// </summary>
        public int? OrderQuantity { get; set; }

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
        /// Required for MK_EX_TYPE_STOPMARKET,
        /// MK_EX_TYPE_STOPLIMIT.
        /// </summary>
        public string? StopPrice { get; set; }

        /// <summary>
        /// If set to TRUE this will cause the order to be removed from the R3 message window and position server once it goes out.
        /// The order will only disappear if a fill is not present on the order.
        /// </summary>
        public bool? RemoveOnOut { get; set; }

        /// <summary>
        /// If set to TRUE you will override the default Easy to Borrow (ETB) handling logic.
        /// </summary>
        public string? EtbOverride { get; set; }

        /// <summary>
        /// If this subscription is dropped, automatically cancel all orders.
        /// This is typically used to cancel an order after disconnection occurs.
        /// See section 7.3 for a more detailed description.
        /// </summary>
        public string? CancelSubscription { get; set; }

        /// <summary>
        /// Trading strategy.
        /// This is not supported on all exchanges.
        /// </summary>
        public Strategy? Strategy { get; set; }

        /// <summary>
        /// Display quantity.
        /// </summary>
        public int? DisplayQty { get; set; }

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_STRATEGY types.
        /// </summary>
        public string? StartTime { get; set; }

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_STRATEGY types.
        /// </summary>
        public string? EndTime { get; set; }

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_STRATEGY types.
        /// </summary>
        public string? ExecutionStyle { get; set; }

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_STRATEGY types.
        /// </summary>
        public string? MaxPercentVolume { get; set; }

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_STRATEGY types.
        /// </summary>
        public string? MinPercentVolume { get; set; }

        /// <summary>
        /// If not present, MK_TIF_DAY will be assumed.
        /// </summary>
        public Tif TimeInForce { get; set; } = Tif.DAY;

        /// <summary>
        /// Used in combination with some MK_ORDER_FIELD_TIF types.
        /// </summary>
        public string? TimeInForceDate { get; set; }

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
        /// You may send orders on behalf of another user if you have been given permission within the system.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Support for fix tag RULE80A
        /// </summary>
        public string? ProgramTrade { get; set; }

        /// <summary>
        /// A random qty not greater than this field value will be added/subtracted to/from MK_ORDER_FIELD_DISPLAY_QTY to randomize display amount.
        /// This field is only valid for the ISE.
        /// </summary>
        public string? DisplayRandom { get; set; }

        /// <summary>
        /// When the displayed quantity should be refreshed. This field is only valid for the ISE.
        /// Valid values are:
        /// MK_DISPLAY_WHEN_STANDARD = standard orders(not reserve)
        /// MK_DISPLAY_WHEN_IMMEDIATE = refresh display after each trade
        /// MK_DISPLAY_WHEN_EXHAUST = refresh display after entire displayed quantity is traded
        /// </summary>
        public DisplayWhen? DisplayWhen { get; set; }
    }
}
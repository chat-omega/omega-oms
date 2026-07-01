using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Requests
{
    public class BasketOrderRow
    {
        /// <summary>
        /// Date of expiration for an option
        /// </summary>
        public DateTime ExpirDate { get; set; }

        /// <summary>
        /// Date of most recent news story
        /// </summary>
        public DateTime NewsDate { get; set; }

        /// <summary>
        /// Allocated value
        /// </summary>
        public double AllocatedValue { get; set; }
        /// <summary>
        /// Average execution price for order so far
        /// </summary>
        public double AvgPrice { get; set; }
        /// <summary>
        /// Currency value of a unit move
        /// </summary>
        public double Basisvalue { get; set; }
        /// <summary>
        /// Net of commissions for today's trades for a symbol
        /// </summary>
        public double Commission { get; set; }
        /// <summary>
        /// ECN Fee
        /// </summary>
        public double EcnFee { get; set; }
        /// <summary>
        /// Latency
        /// </summary>
        public double Latency3 { get; set; }
        /// <summary>
        /// Latency
        /// </summary>
        public double Latency6 { get; set; }
        /// <summary>
        /// Pair cash component
        /// </summary>
        public double PairCash { get; set; }
        /// <summary>
        /// Pair imbalance limit
        /// </summary>
        public double PairImbalanceLimit { get; set; }
        /// <summary>
        /// Pair Leg 1 Benchmark
        /// </summary>
        public double PairLeg1Benchmark { get; set; }
        /// <summary>
        /// Pair Leg 2 Benchmark
        /// </summary>
        public double PairLeg2Benchmark { get; set; }
        /// <summary>
        /// Pair Ratio
        /// </summary>
        public double PairRatio { get; set; }
        /// <summary>
        /// Pair Spread
        /// </summary>
        public double PairSpread { get; set; }
        /// <summary>
        /// Pair Target
        /// </summary>
        public double PairTarget { get; set; }
        /// <summary>
        /// Spread clip
        /// </summary>
        public double SpreadClip { get; set; }

        /// <summary>
        /// Type of account for a position
        /// </summary>
        public int AcctType { get; set; }
        /// <summary>
        /// Book ID
        /// </summary>
        public int BookId { get; set; }
        /// <summary>
        /// Commission Rate Type
        /// </summary>
        public int CommissionRateType { get; set; }
        /// <summary>
        /// Cross flag
        /// </summary>
        public int CrossFlag { get; set; }
        /// <summary>
        /// Date index
        /// </summary>
        public int DateIndex { get; set; }
        /// <summary>
        /// Execution state
        /// </summary>
        public int ExecutionState { get; set; }
        /// <summary>
        /// Extended state information for internal use
        /// </summary>
        public int ExtendedStateFlags { get; set; }
        /// <summary>
        /// Extended state flags 2
        /// </summary>
        public int ExtendedStateFlags2 { get; set; }
        /// <summary>
        /// External acceptance flag
        /// </summary>
        public int ExternalAcceptanceFlag { get; set; }
        /// <summary>
        /// Forex Source
        /// </summary>
        public int FornexSourceFlags { get; set; }
        /// <summary>
        /// Gateway book sequence number
        /// </summary>
        public int GwBookSeqNo { get; set; }
        /// <summary>
        /// Linked Order Cancellation
        /// </summary>
        public int LinkedOrderCancellation { get; set; }
        /// <summary>
        /// Linked Order Relationship
        /// </summary>
        public int LinkedOrderRelationship { get; set; }
        /// <summary>
        /// Minimum movement
        /// </summary>
        public int Minmove { get; set; }
        /// <summary>
        /// OMS client type
        /// </summary>
        public int OmsClientType { get; set; }
        /// <summary>
        /// Order Flags
        /// </summary>
        public int OrderFlags { get; set; }
        /// <summary>
        /// Order flag 2
        /// </summary>
        public int OrderFlags2 { get; set; }
        /// <summary>
        /// Residual volume
        /// </summary>
        public int OrderResidual { get; set; }
        /// <summary>
        /// Original volume of trade
        /// </summary>
        public int OriginalVolume { get; set; }
        /// <summary>
        /// Pair imbalance limit type
        /// </summary>
        public int PairImbalanceLimitType { get; set; }
        /// <summary>
        /// Pair Leg 1 Benchmark
        /// </summary>
        public int PairLeg1BenchmarkType { get; set; }
        /// <summary>
        /// Pair Leg 2 Benchmark
        /// </summary>
        public int PairLeg2BenchmarkType { get; set; }
        /// <summary>
        /// Pair Spread Type
        /// </summary>
        public int PairSpreadType { get; set; }
        /// <summary>
        /// Rank
        /// </summary>
        public int Rank { get; set; }
        /// <summary>
        /// Remaining volume
        /// </summary>
        public int RemainingVolume { get; set; }
        /// <summary>
        /// Shares Allocated
        /// </summary>
        public int SharesAllocated { get; set; }
        /// <summary>
        /// Spread Clip Type
        /// </summary>
        public int SpreadClipType { get; set; }
        /// <summary>
        /// Number of legs this spread contains
        /// </summary>
        public int SpreadLegCount { get; set; }
        /// <summary>
        /// Spread Leg Lean Priority
        /// </summary>
        public int SpreadLegLeanPriority { get; set; }
        /// <summary>
        /// Number of legs this spread contains
        /// </summary>
        public int SpreadLegNumber { get; set; }
        /// <summary>
        /// Spread Leg Price Type
        /// </summary>
        public int SpreadLegPriceType { get; set; }
        /// <summary>
        /// Spread Number of Legs
        /// </summary>
        public int SpreadNumLegs { get; set; }
        /// <summary>
        /// Security Type
        /// </summary>
        public int Styp { get; set; }
        /// <summary>
        /// Account ID
        /// </summary>
        public int TboAccountId { get; set; }
        /// <summary>
        /// UTC Offset
        /// </summary>
        public int UtcOffset { get; set; }
        /// <summary>
        /// Order quantity
        /// </summary>
        public int Volume { get; set; }
        /// <summary>
        /// Volume traded so far by the order
        /// </summary>
        public int VolumeTraded { get; set; }
        /// <summary>
        /// Quantity out in the market
        /// </summary>
        public int WorkingQty { get; set; }

        /// <summary>
        /// Buy/Sell(BUY, SELL, or SELLSHORT)
        /// </summary>
        public Enums.Side Buyorsell { get; set; }
        /// <summary>
        /// Price type submitted in order
        /// </summary>
        public Enums.OrderType PriceType { get; set; }

        /// <summary>
        /// Last ask price for symbol
        /// </summary>
        public string? Ask { get; set; }
        /// <summary>
        /// Last bid price for symbol
        /// </summary>
        public string? Bid { get; set; }
        /// <summary>
        /// Original Price
        /// </summary>
        public string? OriginalPrice { get; set; }
        /// <summary>
        /// Limit price submitted in order
        /// </summary>
        public string? Price { get; set; }
        /// <summary>
        /// Stop Price
        /// </summary>
        public string? StopPrice { get; set; }
        /// <summary>
        /// Price at which option can be exercised
        /// </summary>
        public string? StrikePrc { get; set; }
        /// <summary>
        /// Bank of trading account for selected position
        /// </summary>
        public string? Bank { get; set; }
        /// <summary>
        /// Branch of trading account for selected position
        /// </summary>
        public string? Branch { get; set; }
        /// <summary>
        /// Claimed By Clerk
        /// </summary>
        public string? ClaimedByClerk { get; set; }
        /// <summary>
        /// Client Order ID
        /// </summary>
        public string? ClientOrderId { get; set; }
        /// <summary>
        /// Broker Code
        /// </summary>
        public string? CommissionCode { get; set; }
        /// <summary>
        /// Currency of symbol being traded or Currency of Position
        /// </summary>
        public string? Currency { get; set; }
        /// <summary>
        /// Current status of order(PENDING, LIVE, COMPLETED, or DELETED)
        /// </summary>
        public string? CurrentStatus { get; set; }
        /// <summary>
        /// Customer name of trading account for selected position
        /// </summary>
        public string? Customer { get; set; }
        /// <summary>
        /// Deposit or account name for selected position
        /// </summary>
        public string? Deposit { get; set; }
        /// <summary>
        /// 'IBM' (Symbol formats are datafeed-specific.Check your Feed Handler help file for the correct format.)
        /// </summary>
        public string? DispName { get; set; }
        /// <summary>
        /// Exchange of symbol(uses same as EXCH_ NAME)
        /// </summary>
        public string? Exchange { get; set; }

        /// <summary>
        /// Route name as shown in Eze EMS
        /// </summary>
        public string? ExitVehicle { get; set; }
        /// <summary>
        /// FIX Trader ID
        /// </summary>
        public string? FixTraderId { get; set; }
        /// <summary>
        /// Time at which order is first valid for execution
        /// </summary>
        public string? GoodFrom { get; set; }
        /// <summary>
        /// Time at which order is no longer valid(DAY, DAYPLUS, or custom value supported by destination)
        /// </summary>
        public string? GoodUntil { get; set; }
        /// <summary>
        /// ID of Linked Order
        /// </summary>
        public string? LinkedOrderId { get; set; }
        /// <summary>
        /// New Remote Identification Code
        /// </summary>
        public string? NewRemoteId { get; set; }
        /// <summary>
        /// Contra(used by ARCAEX to signify liquidity added or removed)
        /// </summary>
        public string? OppositeParty { get; set; }
        /// <summary>
        /// A unique id associated to every order.This is the identifier to lookup the specific order to cancel
        /// </summary>
        public string? OrderId { get; set; }
        /// <summary>
        /// Order Tag
        /// </summary>
        public string? OrderTag { get; set; }
        /// <summary>
        /// Original Order Identification Code
        /// </summary>
        public string? OriginalOrderId { get; set; }
        /// <summary>
        /// USERNAME@DOMAIN of the trader who placed the trade originally
        /// </summary>
        public string? OriginalTraderId { get; set; }
        /// <summary>
        /// Option type (P = put, C = call, U = underlier) for symbol
        /// </summary>
        public string? Putcallind { get; set; }
        /// <summary>
        /// Reason given by user or destination for changing, cancelling, or deleting the order
        /// </summary>
        public string? Reason { get; set; }
        /// <summary>
        /// Refers to Identification Code
        /// </summary>
        public string? RefersToId { get; set; }
        /// <summary>
        /// Remote ID
        /// </summary>
        public string? RemoteId { get; set; }
        /// <summary>
        /// ID assigned to short sell orders as required by Regulation SHO
        /// </summary>
        public string? ShortLocateId { get; set; }
        /// <summary>
        /// Table
        /// </summary>
        public string? Table { get; set; }
        /// <summary>
        /// Ticket ID
        /// </summary>
        public string? TicketId { get; set; }
        /// <summary>
        /// Timestamp applied to order by trading system
        /// </summary>
        public string? TimeStamp { get; set; }
        /// <summary>
        /// Trader capacity
        /// </summary>
        public string? TraderCapacity { get; set; }
        /// <summary>
        /// USERNAME@DOMAIN of the message recipient
        /// </summary>
        public string? TraderId { get; set; }
        /// <summary>
        /// Order Event Type
        /// </summary>
        public string? Type { get; set; }
        /// <summary>
        /// Underlier symbol
        /// </summary>
        public string? Undersym { get; set; }
        /// <summary>
        /// User Message/Notes
        /// </summary>
        public string? UserMessage { get; set; }
        /// <summary>
        /// Indicates the fill type? partial, AON, SPZ, or Imbalance? of the position for the symbol
        /// </summary>
        public string? VolumeType { get; set; }
        /// <summary>
        /// Route
        /// </summary>
        public string? Route { get; set; }

        /// <summary>
        /// Time of most recent news story
        /// </summary>
        public TimeSpan NewsTime { get; set; }
        /// <summary>
        /// Time of last trade
        /// </summary>
        public TimeSpan TrdTime { get; set; }

        /// <summary>
        /// Extended Fields
        /// </summary>
        public Dictionary<string, string> ExtendedFields { get; set; } = new Dictionary<string, string>();
    }
}

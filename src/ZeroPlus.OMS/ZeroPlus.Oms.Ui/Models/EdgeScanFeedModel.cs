using DevExpress.Mvvm;
using NLog;
using SymbolLib;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class EdgeScanFeedModel : BindableBase, IEdgeScanFeedModel, IPriceChainModel
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly TimeSpan _lastSnapshot = TimeSpan.FromHours(14) + TimeSpan.FromMinutes(50);

        private int _multiplier = 100;
        private byte _legsCount;
        private string _toStringCache;

        private BasketTraderItemModel _basketItem;
        public readonly DateTime CreationTime;
        private Timer _timer;

        public DateTime ReceiveTime { get; internal set; }
        public string SessionId { get; set; }
        public bool IsFirm { get; set; }
        public bool PossibleFirm { get; set; }
        public bool PossibleCopyCat { get; set; }
        public bool Uncertain { get; set; }
        public bool QtyMismatch { get; set; }
        public Side? Side { get; set; }
        public string UnderSymbol { get; set; }
        public string Description { get; set; }
        public string SpreadId { get; set; }
        public double Ttl { get; set; }
        public string Exchange { get; set; }
        public string SpreadType { get; set; }
        public double Position { get; set; }
        public double AdjustedPnl { get; set; }
        public ushort BuyQty { get; set; }
        public ushort SellQty { get; set; }
        public double BuyPrice { get; set; }
        public double BuyTradeOriginalPrice { get; set; }
        public ushort BuyBidSize { get; set; }
        public ushort BuyAskSize { get; set; }
        public double SellPrice { get; set; }
        public double SellTradeOriginalPrice { get; set; }
        public ushort SellBidSize { get; set; }
        public ushort SellAskSize { get; set; }
        public double Width { get; set; }
        public double DeltaAdjEdge { get; set; }
        public double HighestLegDelta { get; set; }
        public double SpreadWeightedVega { get; set; }
        public double BuyEdgeToTheo { get; set; }
        public double BuyVolaEdgeToTheo { get; set; }
        public double BuyMinEdgeToTheo => Math.Min(BuyEdgeToTheo, BuyVolaEdgeToTheo);
        public double SellEdgeToTheo { get; set; }
        public double SellVolaEdgeToTheo { get; set; }
        public double SellMinEdgeToTheo => Math.Min(SellEdgeToTheo, SellVolaEdgeToTheo);
        public bool BuyFirst => EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence && BuyTime < SellTime;
        public bool SellFirst => EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence && BuyTime > SellTime;
        public DateTime BuyTime { get; set; }
        public DateTime SellTime { get; set; }
        public string BuySymbol { get; set; }
        public string SellSymbol { get; set; }
        public DateTime NearExpiration { get; set; }
        public DateTime FarExpiration { get; set; }
        public bool Mleg { get; set; }
        public TimeSpan Latency { get; set; }
        public TimeSpan FinderLatency { get; set; }
        public EdgeScannerType EdgeScannerType { get; set; }
        public ushort FlipCount { get; set; }
        public double SpreadWidth { get; set; }
        public double AbsDelta { get; set; }
        public double BuyTradeBid { get; set; }
        public double BuyTradeMid { get; set; }
        public double BuyTradeAsk { get; set; }
        public double BuyTradeTheo { get; set; }
        public double BuyTradeDelta { get; set; }
        public double BuyTradeUnderlyingMid { get; set; }
        public double SellTradeUnderlyingMid { get; set; }
        public double SellTradeBid { get; set; }
        public double SellTradeMid { get; set; }
        public double SellTradeAsk { get; set; }
        public double SellTradeTheo { get; set; }
        public double SellTradeDelta { get; set; }
        public double BuyBidPercent { get; set; }
        public double SellBidPercent { get; set; }
        public char BuyConditionCode { get; set; }
        public char SellConditionCode { get; set; }
        public double BuyUnderlyingWidth { get; set; }
        public double SellUnderlyingWidth { get; set; }
        public Side AdjSide { get; set; }
        public Side IbCobSide { get; set; }
        public double IbCobBid { get; set; }
        public double IbCobAsk { get; set; }
        public double IvPctChange { get; set; } = double.NaN;
        public bool Traded { get; internal set; }
        public string ExtraTag { get; set; }
        public int PriceChainTotalBidDeviations { get; set; }
        public int PriceChainTotalAskDeviations { get; set; }
        public int PriceChainDeviationSequence { get; set; }
        public double PriceChainTradePrice { get; set; }
        public double PriceChainRecentBidDeviation { get; set; }
        public double PriceChainRecentBidDeviationTimeDiff { get; set; }
        public double PriceChainRecentBidDeviationUnderBid { get; set; }
        public double PriceChainRecentBidDeviationUnderMid => (PriceChainRecentBidDeviationUnderBid + PriceChainRecentBidDeviationUnderAsk) / 2;
        public double PriceChainRecentBidDeviationUnderWidth => PriceChainRecentBidDeviationUnderAsk - PriceChainRecentBidDeviationUnderBid;
        public double PriceChainRecentBidDeviationUnderAsk { get; set; }
        public double PriceChainRecentBidDeviationBid { get; set; }
        public double PriceChainRecentBidDeviationMid => (PriceChainRecentBidDeviationBid + PriceChainRecentBidDeviationAsk) / 2;
        public double PriceChainRecentBidDeviationWidth => PriceChainRecentBidDeviationAsk - PriceChainRecentBidDeviationBid;
        public double PriceChainRecentBidDeviationAsk { get; set; }
        public double PriceChainRecentAskDeviation { get; set; }
        public double PriceChainRecentAskDeviationTimeDiff { get; set; }
        public double PriceChainRecentAskDeviationUnderBid { get; set; }
        public double PriceChainRecentAskDeviationUnderMid => (PriceChainRecentAskDeviationUnderBid + PriceChainRecentAskDeviationUnderAsk) / 2;
        public double PriceChainRecentAskDeviationUnderWidth => PriceChainRecentAskDeviationUnderAsk - PriceChainRecentAskDeviationUnderBid;
        public double PriceChainRecentAskDeviationUnderAsk { get; set; }
        public double PriceChainRecentAskDeviationBid { get; set; }
        public double PriceChainRecentAskDeviationMid => (PriceChainRecentAskDeviationBid + PriceChainRecentAskDeviationAsk) / 2;
        public double PriceChainRecentAskDeviationWidth => PriceChainRecentAskDeviationAsk - PriceChainRecentAskDeviationBid;
        public double PriceChainRecentAskDeviationAsk { get; set; }
        public double PriceChainHighestBidDeviation { get; set; }
        public double PriceChainHighestBidDeviationTimeDiff { get; set; }
        public double PriceChainHighestBidDeviationUnderBid { get; set; }
        public double PriceChainHighestBidDeviationUnderMid => (PriceChainHighestBidDeviationUnderBid + PriceChainHighestBidDeviationUnderAsk) / 2;
        public double PriceChainHighestBidDeviationUnderWidth => PriceChainHighestBidDeviationUnderAsk - PriceChainHighestBidDeviationUnderBid;
        public double PriceChainHighestBidDeviationUnderAsk { get; set; }
        public double PriceChainHighestBidDeviationBid { get; set; }
        public double PriceChainHighestBidDeviationMid => (PriceChainHighestBidDeviationBid + PriceChainHighestBidDeviationAsk) / 2;
        public double PriceChainHighestBidDeviationWidth => PriceChainHighestBidDeviationAsk - PriceChainHighestBidDeviationBid;
        public double PriceChainHighestBidDeviationAsk { get; set; }
        public double PriceChainHighestAskDeviation { get; set; }
        public double PriceChainHighestAskDeviationTimeDiff { get; set; }
        public double PriceChainHighestAskDeviationUnderBid { get; set; }
        public double PriceChainHighestAskDeviationUnderMid => (PriceChainHighestAskDeviationUnderBid + PriceChainHighestAskDeviationUnderAsk) / 2;
        public double PriceChainHighestAskDeviationUnderWidth => PriceChainHighestAskDeviationUnderAsk - PriceChainHighestAskDeviationUnderBid;
        public double PriceChainHighestAskDeviationUnderAsk { get; set; }
        public double PriceChainHighestAskDeviationBid { get; set; }
        public double PriceChainHighestAskDeviationMid => (PriceChainHighestAskDeviationBid + PriceChainHighestAskDeviationAsk) / 2;
        public double PriceChainHighestAskDeviationWidth => PriceChainHighestAskDeviationAsk - PriceChainHighestAskDeviationBid;
        public double PriceChainHighestAskDeviationAsk { get; set; }
        public double PriceChainRecentBidDeviationIvOffset { get; set; }
        public double PriceChainHighestBidDeviationIvOffset { get; set; }
        public double PriceChainRecentAskDeviationIvOffset { get; set; }
        public double PriceChainHighestAskDeviationIvOffset { get; set; }
        public double ReceiveLatency { get; set; }
        public Side? EvalSide { get; set; }
        public Side? BestSweepSide { get; set; }
        public string Condition => GetConditionCode(BuyConditionCode);
        public string SellCondition => GetConditionCode(SellConditionCode);
        [Bindable]
        public partial bool OrderSent { get; set; }
        [Bindable]
        public partial bool OrderFilled { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial string Reason { get; set; }
        public byte LegsCount
        {
            get => _legsCount;
            set
            {
                _legsCount = value;
                Mleg = value > 1;
            }
        }
        [Bindable]
        public partial PositionEffect? PositionEffect { get; set; }
        [Bindable]
        public partial bool ShowPriceNotification { get; set; }
        [Bindable]
        public partial EdgeScanStatLogger Logger { get; set; }

        public double Notional => EdgeScannerType == EdgeScannerType.SweepFinder ? BuyQty * BuyPrice * _multiplier : double.NaN;

        public EdgeScanFeedModel()
        {
            CreationTime = DateTime.Now;
            ReceiveTime = DateTime.Now;
        }

        public EdgeScanFeedModel(EdgeScanFeedModel feed, bool invert = false)
        {
            ReceiveTime = feed.ReceiveTime;
            CreationTime = feed.CreationTime;
            EdgeScannerType = feed.EdgeScannerType;
            BuyQty = feed.BuyQty;
            SellQty = feed.SellQty;
            BuyBidSize = feed.BuyBidSize;
            BuyAskSize = feed.BuyAskSize;
            SellBidSize = feed.SellBidSize;
            SellAskSize = feed.SellAskSize;
            LegsCount = feed.LegsCount;
            BuyPrice = feed.BuyPrice;
            BuyTradeOriginalPrice = feed.BuyTradeOriginalPrice;
            SellPrice = feed.SellPrice;
            SellTradeOriginalPrice = feed.SellTradeOriginalPrice;
            Width = Math.Abs(Math.Abs(BuyPrice) - Math.Abs(SellPrice));
            BuyEdgeToTheo = feed.BuyEdgeToTheo;
            BuyVolaEdgeToTheo = feed.BuyVolaEdgeToTheo;
            SellEdgeToTheo = feed.SellEdgeToTheo;
            SellVolaEdgeToTheo = feed.SellVolaEdgeToTheo;
            BuyTime = feed.BuyTime;
            SellTime = feed.SellTime;
            NearExpiration = feed.NearExpiration;
            FarExpiration = feed.FarExpiration;
            AdjustedPnl = feed.AdjustedPnl;
            Position = feed.Position;
            Ttl = feed.Ttl;
            Exchange = feed.Exchange;
            Description = feed.Description;
            SpreadId = feed.SpreadId;
            SpreadType = feed.SpreadType;
            UnderSymbol = feed.UnderSymbol;
            BuySymbol = feed.BuySymbol;
            SellSymbol = feed.SellSymbol;
            IsFirm = feed.IsFirm;
            PossibleFirm = feed.PossibleFirm;
            PossibleCopyCat = feed.PossibleCopyCat;
            Uncertain = feed.Uncertain;
            QtyMismatch = feed.QtyMismatch;
            FinderLatency = feed.FinderLatency;
            Latency = feed.Latency;
            IvPctChange = feed.IvPctChange;

            FlipCount = feed.FlipCount;
            SpreadWidth = feed.SpreadWidth;
            BuyTradeBid = feed.BuyTradeBid;
            BuyTradeMid = feed.BuyTradeMid;
            BuyTradeAsk = feed.BuyTradeAsk;
            BuyTradeTheo = feed.BuyTradeTheo;
            BuyTradeDelta = feed.BuyTradeDelta;
            SellTradeBid = feed.SellTradeBid;
            SellTradeMid = feed.SellTradeMid;
            SellTradeAsk = feed.SellTradeAsk;
            SellTradeTheo = feed.SellTradeTheo;
            SellTradeDelta = feed.SellTradeDelta;
            BuyBidPercent = feed.BuyBidPercent;
            ReceiveLatency = feed.ReceiveLatency;

            BuyTradeUnderlyingMid = feed.BuyTradeUnderlyingMid;
            SellTradeUnderlyingMid = feed.SellTradeUnderlyingMid;

            BuyUnderlyingWidth = feed.BuyUnderlyingWidth;
            SellUnderlyingWidth = feed.SellUnderlyingWidth;

            PriceChainTotalBidDeviations = feed.PriceChainTotalBidDeviations;
            PriceChainTotalAskDeviations = feed.PriceChainTotalAskDeviations;
            PriceChainTradePrice = feed.PriceChainTradePrice;
            PriceChainDeviationSequence = feed.PriceChainDeviationSequence;
            PriceChainRecentBidDeviation = feed.PriceChainRecentBidDeviation;
            PriceChainRecentBidDeviationTimeDiff = feed.PriceChainRecentBidDeviationTimeDiff;
            PriceChainRecentBidDeviationUnderBid = feed.PriceChainRecentBidDeviationUnderBid;
            PriceChainRecentBidDeviationUnderAsk = feed.PriceChainRecentBidDeviationUnderAsk;
            PriceChainRecentBidDeviationBid = feed.PriceChainRecentBidDeviationBid;
            PriceChainRecentBidDeviationAsk = feed.PriceChainRecentBidDeviationAsk;
            PriceChainRecentAskDeviation = feed.PriceChainRecentAskDeviation;
            PriceChainRecentAskDeviationTimeDiff = feed.PriceChainRecentAskDeviationTimeDiff;
            PriceChainRecentAskDeviationUnderBid = feed.PriceChainRecentAskDeviationUnderBid;
            PriceChainRecentAskDeviationUnderAsk = feed.PriceChainRecentAskDeviationUnderAsk;
            PriceChainRecentAskDeviationBid = feed.PriceChainRecentAskDeviationBid;
            PriceChainRecentAskDeviationAsk = feed.PriceChainRecentAskDeviationAsk;
            PriceChainHighestBidDeviation = feed.PriceChainHighestBidDeviation;
            PriceChainHighestBidDeviationTimeDiff = feed.PriceChainHighestBidDeviationTimeDiff;
            PriceChainHighestBidDeviationUnderBid = feed.PriceChainHighestBidDeviationUnderBid;
            PriceChainHighestBidDeviationUnderAsk = feed.PriceChainHighestBidDeviationUnderAsk;
            PriceChainHighestBidDeviationBid = feed.PriceChainHighestBidDeviationBid;
            PriceChainHighestBidDeviationAsk = feed.PriceChainHighestBidDeviationAsk;
            PriceChainHighestAskDeviation = feed.PriceChainHighestAskDeviation;
            PriceChainHighestAskDeviationTimeDiff = feed.PriceChainHighestAskDeviationTimeDiff;
            PriceChainHighestAskDeviationUnderBid = feed.PriceChainHighestAskDeviationUnderBid;
            PriceChainHighestAskDeviationUnderAsk = feed.PriceChainHighestAskDeviationUnderAsk;
            PriceChainHighestAskDeviationBid = feed.PriceChainHighestAskDeviationBid;
            PriceChainHighestAskDeviationAsk = feed.PriceChainHighestAskDeviationAsk;

            PriceChainRecentBidDeviationIvOffset = feed.PriceChainRecentBidDeviationIvOffset;
            PriceChainHighestBidDeviationIvOffset = feed.PriceChainHighestBidDeviationIvOffset;
            PriceChainRecentAskDeviationIvOffset = feed.PriceChainRecentAskDeviationIvOffset;
            PriceChainHighestAskDeviationIvOffset = feed.PriceChainHighestAskDeviationIvOffset;
            AdjSide = feed.AdjSide;

            if (feed.EdgeScannerType == EdgeScannerType.SweepFinder)
            {
                IbCobSide = feed.IbCobSide;
                IbCobBid = feed.IbCobBid;
                IbCobAsk = feed.IbCobAsk;

                if (feed.BuyBidPercent > .75 && feed.BuyEdgeToTheo < 0)
                {
                    EvalSide = ZeroPlus.Models.Data.Enums.Side.Buy;
                }
                else if (feed.BuyBidPercent < .25 && feed.BuyEdgeToTheo > 0)
                {
                    EvalSide = ZeroPlus.Models.Data.Enums.Side.Sell;
                }

                int buys = 0;
                if (AdjSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    buys++;
                }
                if (EvalSide != null && EvalSide!.Value == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    buys++;
                }
                if (IbCobSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    buys++;
                }

                BestSweepSide = buys >= 2 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
            }

            BuyConditionCode = feed.BuyConditionCode;
            SellConditionCode = feed.SellConditionCode;

            AbsDelta = feed.AbsDelta;

            BuyBidPercent = feed.BuyBidPercent;
            SellBidPercent = feed.SellBidPercent;

            ExtraTag = feed.ExtraTag;
            DeltaAdjEdge = feed.DeltaAdjEdge;
            HighestLegDelta = feed.HighestLegDelta;
            SpreadWeightedVega = feed.SpreadWeightedVega;
            if (invert)
            {
                Side = feed.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                Message = "Duplicate Feed";
            }
            else
            {
                Side = feed.Side;
                Message = feed.Message;
            }
        }

        internal OmsOrder ToOrder()
        {
            bool useBuySide = BuyEdgeToTheo >= SellEdgeToTheo || double.IsNaN(SellEdgeToTheo);
            OmsOrder omsOrder = new()
            {
                UnderlyingSymbol = UnderSymbol,
                PositionEffect = "Auto",
            };
            if (useBuySide)
            {
                omsOrder.Symbol = BuySymbol;
                omsOrder.Price = BuyPrice;
                omsOrder.SideString = ZeroPlus.Models.Data.Enums.Side.Buy.ToString();
                omsOrder.Side = ZeroPlus.Models.Data.Enums.Side.Buy;
            }
            else
            {
                omsOrder.Symbol = SellSymbol;
                omsOrder.Price = SellPrice;
                omsOrder.SideString = ZeroPlus.Models.Data.Enums.Side.Sell.ToString();
                omsOrder.Side = ZeroPlus.Models.Data.Enums.Side.Sell;
            }
            if (Mleg)
            {
                SymbolCodec decoded = new(useBuySide ? BuySymbol : SellSymbol);
                if (decoded.LegCount > 1)
                {
                    for (int i = 0; i < decoded.LegCount; i++)
                    {
                        Instrument leg = decoded.GetLeg(i);
                        omsOrder.Legs.Add(new OmsOrderLeg()
                        {
                            Symbol = leg.symbol,
                            Side = leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                            Quantity = leg.ratio,
                            Ratio = leg.ratio,
                        });
                    }
                }
            }
            return omsOrder;
        }

        internal void SetupLogger(BasketTraderItemModel basketItem, bool fullLog)
        {
            try
            {
                Logger = new();
                _basketItem = basketItem;
                if (fullLog)
                {
                    _ = _basketItem.WaitForMarkLoad().ContinueWith(_ =>
                    {
                        _timer = new Timer
                        {
                            AutoReset = false,
                            Interval = 1 * 60 * 1000
                        };

                        TimeSpan now = DateTime.Now.TimeOfDay;
                        TimeSpan nextInterval = now + TimeSpan.FromMilliseconds(_timer.Interval);
                        if (nextInterval > _lastSnapshot)
                        {
                            SetUpFinalLog();
                        }
                        else
                        {
                            _timer.Elapsed += Set1MinuteValues;
                            _timer.Start();
                        }

                        Logger.MidAtEntry = _basketItem.Mid;
                        if (BuyEdgeToTheo > 0)
                        {
                            Logger.PnlAtEntry = Math.Round((Math.Abs(Logger.MidAtEntry) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                        }
                        else if (BuyEdgeToTheo < 0)
                        {
                            Logger.PnlAtEntry = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAtEntry)) * _basketItem.Multiplier, 2);
                        }
                    });
                    _ = _basketItem.WaitForUnderMidLoadAsync().ContinueWith(task =>
                    {
                        if (task.Result)
                        {
                            Logger.UnderlyingAtEntry = _basketItem.UnderMid;
                        }
                    });
                    _ = _basketItem.WaitForAdjTheoLoadAsync().ContinueWith(task =>
                    {
                        if (task.Result)
                        {
                            Logger.DeltaAdjTheoAtEntry = _basketItem.NetDeltaAdjTheo;
                        }
                    });
                }
                _ = _basketItem.WaitForTheoLoadAsync().ContinueWith(async task =>
                {
                    if (task.Result)
                    {
                        Logger.VegaAtEntry = _basketItem.TotalVega;
                        Logger.DeltaAtEntry = _basketItem.TotalDelta;
                        Logger.GammaAtEntry = _basketItem.TotalGamma;
                        Logger.ImpliedAtEntry = _basketItem.TotalImplied;

                        TicketLegModel itmLeg = _basketItem.Legs.MaxBy(x => Math.Abs(x.Delta));
                        if (itmLeg != null)
                        {
                            await Task.Delay(OrderTicket.DataLoadTimeout).ContinueWith(_ =>
                            {
                                if (!double.IsNaN(itmLeg.Volume) && !double.IsNaN(itmLeg.OpenInterest))
                                {
                                    PositionEffect = itmLeg.Volume > itmLeg.OpenInterest ? ZeroPlus.Models.Data.Enums.PositionEffect.Open : ZeroPlus.Models.Data.Enums.PositionEffect.Close;
                                }
                            });
                        }
                        if (!fullLog)
                        {
                            _basketItem.Dispose();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetupLogger));
            }
        }

        private void SetUpFinalLog()
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            if (_lastSnapshot > now)
            {
                _timer.Interval = (_lastSnapshot - now).TotalMilliseconds;
                _timer.Elapsed += SetFinalValues;
                _timer.Start();
            }
            else
            {
                _basketItem.Dispose();
                _timer.Dispose();
            }
        }

        private void Set1MinuteValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAt1Minute = _basketItem.Mid;
                Logger.UnderlyingAt1Minute = _basketItem.UnderMid;
                Logger.VegaAt1Minute = _basketItem.TotalVega;
                Logger.DeltaAt1Minute = _basketItem.TotalDelta;
                Logger.GammaAt1Minute = _basketItem.TotalGamma;
                Logger.ImpliedAt1Minute = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAt1Minute = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAt1Minute = Math.Round((Math.Abs(Logger.MidAt1Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAt1Minute = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt1Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Interval = 5 * 60 * 1000;
                _timer.Elapsed -= Set1MinuteValues;

                TimeSpan now = DateTime.Now.TimeOfDay;
                TimeSpan nextInterval = now + TimeSpan.FromMilliseconds(_timer.Interval);
                if (nextInterval > _lastSnapshot)
                {
                    SetUpFinalLog();
                }
                else
                {
                    _timer.Elapsed += Set5MinuteValues;
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set1MinuteValues));
            }
        }

        private void Set5MinuteValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAt5Minute = _basketItem.Mid;
                Logger.UnderlyingAt5Minute = _basketItem.UnderMid;
                Logger.VegaAt5Minute = _basketItem.TotalVega;
                Logger.DeltaAt5Minute = _basketItem.TotalDelta;
                Logger.GammaAt5Minute = _basketItem.TotalGamma;
                Logger.ImpliedAt5Minute = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAt5Minute = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAt5Minute = Math.Round((Math.Abs(Logger.MidAt5Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAt5Minute = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt5Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Interval = 10 * 60 * 1000;
                _timer.Elapsed -= Set5MinuteValues;

                TimeSpan now = DateTime.Now.TimeOfDay;
                TimeSpan nextInterval = now + TimeSpan.FromMilliseconds(_timer.Interval);
                if (nextInterval > _lastSnapshot)
                {
                    SetUpFinalLog();
                }
                else
                {
                    _timer.Elapsed += Set10MinuteValues;
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set5MinuteValues));
            }
        }

        private void Set10MinuteValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAt10Minute = _basketItem.Mid;
                Logger.UnderlyingAt10Minute = _basketItem.UnderMid;
                Logger.VegaAt10Minute = _basketItem.TotalVega;
                Logger.DeltaAt10Minute = _basketItem.TotalDelta;
                Logger.GammaAt10Minute = _basketItem.TotalGamma;
                Logger.ImpliedAt10Minute = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAt10Minute = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAt10Minute = Math.Round((Math.Abs(Logger.MidAt10Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAt10Minute = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt10Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Interval = 30 * 60 * 1000;
                _timer.Elapsed -= Set10MinuteValues;

                TimeSpan now = DateTime.Now.TimeOfDay;
                TimeSpan nextInterval = now + TimeSpan.FromMilliseconds(_timer.Interval);
                if (nextInterval > _lastSnapshot)
                {
                    SetUpFinalLog();
                }
                else
                {
                    _timer.Elapsed += Set30MinuteValues;
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set10MinuteValues));
            }
        }

        private void Set30MinuteValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAt30Minute = _basketItem.Mid;
                Logger.UnderlyingAt30Minute = _basketItem.UnderMid;
                Logger.VegaAt30Minute = _basketItem.TotalVega;
                Logger.DeltaAt30Minute = _basketItem.TotalDelta;
                Logger.GammaAt30Minute = _basketItem.TotalGamma;
                Logger.ImpliedAt30Minute = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAt30Minute = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAt30Minute = Math.Round((Math.Abs(Logger.MidAt30Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAt30Minute = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt30Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Interval = 60 * 60 * 1000;
                _timer.Elapsed -= Set30MinuteValues;

                TimeSpan now = DateTime.Now.TimeOfDay;
                TimeSpan nextInterval = now + TimeSpan.FromMilliseconds(_timer.Interval);
                if (nextInterval > _lastSnapshot)
                {
                    SetUpFinalLog();
                }
                else
                {
                    _timer.Elapsed += Set60MinuteValues;
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set30MinuteValues));
            }
        }

        private void Set60MinuteValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAt60Minute = _basketItem.Mid;
                Logger.UnderlyingAt60Minute = _basketItem.UnderMid;
                Logger.VegaAt60Minute = _basketItem.TotalVega;
                Logger.DeltaAt60Minute = _basketItem.TotalDelta;
                Logger.GammaAt60Minute = _basketItem.TotalGamma;
                Logger.ImpliedAt60Minute = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAt60Minute = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAt60Minute = Math.Round((Math.Abs(Logger.MidAt60Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAt60Minute = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt60Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Elapsed -= Set60MinuteValues;

                SetUpFinalLog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set60MinuteValues));
            }
        }

        private void SetFinalValues(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.MidAtClose = _basketItem.Mid;
                Logger.UnderlyingAtClose = _basketItem.UnderMid;
                Logger.VegaAtClose = _basketItem.TotalVega;
                Logger.DeltaAtClose = _basketItem.TotalDelta;
                Logger.GammaAtClose = _basketItem.TotalGamma;
                Logger.ImpliedAtClose = _basketItem.TotalImplied;
                Logger.DeltaAdjTheoAtClose = _basketItem.NetDeltaAdjTheo;

                if (BuyEdgeToTheo > 0)
                {
                    Logger.PnlAtClose = Math.Round((Math.Abs(Logger.MidAt60Minute) - Math.Abs(BuyPrice)) * _basketItem.Multiplier, 2);
                }
                else if (BuyEdgeToTheo < 0)
                {
                    Logger.PnlAtClose = Math.Round((Math.Abs(BuyPrice) - Math.Abs(Logger.MidAt60Minute)) * _basketItem.Multiplier, 2);
                }

                _timer.Elapsed -= SetFinalValues;
                _basketItem.Dispose();
                _timer.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Set60MinuteValues));
            }
        }

        public override string ToString()
        {
            try
            {
                _toStringCache ??= ", Underlying: " + UnderSymbol +
                                   ", Description: " + Description +
                                   ", Condition: " + Condition +
                                   ", Buy Px:" + Math.Round(BuyPrice, 2) +
                                   ", Sell Px:" + Math.Round(SellPrice, 2) +
                                   ", Buy ETT:" + Math.Round(BuyEdgeToTheo, 2) +
                                   ", Sell ETT:" + Math.Round(SellEdgeToTheo, 2) +
                                   ", Buy Sym:" + BuySymbol +
                                   ", Sell Sym:" + SellSymbol +
                                   ", Buy Time:" + BuyTime.ToString("hh:mm:ss ffffff") +
                                   ", Sell Time:" + SellTime.ToString("hh:mm:ss ffffff") +
                                   ", Find Latency:" + FinderLatency +
                                   ", Latency:" + Latency +
                                   ", FindLatencyMicro:" + FinderLatency.TotalMicroseconds +
                                   ", LatencyMicro:" + Latency.TotalMicroseconds;
                return _toStringCache;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToString));
                return ex.Message;
            }
        }

        public string GetCsv()
        {
            return $"{ReceiveTime:hh:mm:ss ffffff}, " +
                   $"{IsFirm}, " +
                   $"{PossibleFirm}, " +
                   $"{PossibleCopyCat}, " +
                   $"{Uncertain}, " +
                   $"{QtyMismatch}, " +
                   $"{Side}, " +
                   $"{UnderSymbol}, " +
                   $"{Description}, " +
                   $"{SpreadId}, " +
                   $"{Ttl:F2}, " +
                   $"{Exchange}, " +
                   $"{SpreadType}, " +
                   $"{Position:F0}, " +
                   $"{AdjustedPnl:F2}, " +
                   $"{BuyQty}, " +
                   $"{SellQty}, " +
                   $"{BuyPrice:F2}, " +
                   $"{SellPrice:F2}, " +
                   $"{Width:F2}, " +
                   $"{DeltaAdjEdge:F2}, " +
                   $"{BuyEdgeToTheo:F2}, " +
                   $"{SellEdgeToTheo:F2}, " +
                   $"{BuyFirst}, " +
                   $"{SellFirst}, " +
                   $"{BuyTime:hh:mm:ss ffffff}, " +
                   $"{SellTime:hh:mm:ss ffffff}, " +
                   $"{BuySymbol}, " +
                   $"{SellSymbol}, " +
                   $"{NearExpiration:MM-dd-yy}, " +
                   $"{FarExpiration:MM-dd-yy}, " +
                   $"{Mleg}, " +
                   $"{Latency}, " +
                   $"{FinderLatency}, " +
                   $"{EdgeScannerType}, " +
                   $"{FlipCount}, " +
                   $"{SpreadWidth:F2}, " +
                   $"{AbsDelta:F4}, " +
                   $"{BuyTradeBid:F2}, " +
                   $"{BuyTradeMid:F2}, " +
                   $"{BuyTradeAsk:F2}, " +
                   $"{BuyTradeTheo:F2}, " +
                   $"{BuyTradeDelta:F2}, " +
                   $"{BuyTradeUnderlyingMid:F2}, " +
                   $"{SellTradeUnderlyingMid:F2}, " +
                   $"{SellTradeBid:F2}, " +
                   $"{SellTradeMid:F2}, " +
                   $"{SellTradeAsk:F2}, " +
                   $"{SellTradeTheo:F2}, " +
                   $"{SellTradeDelta:F2}, " +
                   $"{BuyBidPercent:F2}, " +
                   $"{SellBidPercent:F2}, " +
                   $"{BuyConditionCode}, " +
                   $"{SellConditionCode}, " +
                   $"{BuyUnderlyingWidth:F2}, " +
                   $"{SellUnderlyingWidth:F2}, " +
                   $"{Traded}, " +
                   $"{Message}, " +
                   $"{LegsCount}, " +
                   $"{Condition}, \n";
        }

        public static string GetCsvHeader()
        {
            return $"{nameof(ReceiveTime)}, " +
                   $"{nameof(IsFirm)}, " +
                   $"{nameof(PossibleFirm)}, " +
                   $"{nameof(PossibleCopyCat)}, " +
                   $"{nameof(Uncertain)}, " +
                   $"{nameof(QtyMismatch)}, " +
                   $"{nameof(Side)}, " +
                   $"{nameof(UnderSymbol)}, " +
                   $"{nameof(Description)}, " +
                   $"{nameof(SpreadId)}, " +
                   $"{nameof(Ttl)}, " +
                   $"{nameof(Exchange)}, " +
                   $"{nameof(SpreadType)}, " +
                   $"{nameof(Position)}, " +
                   $"{nameof(AdjustedPnl)}, " +
                   $"{nameof(BuyQty)}, " +
                   $"{nameof(SellQty)}, " +
                   $"{nameof(BuyPrice)}, " +
                   $"{nameof(SellPrice)}, " +
                   $"{nameof(Width)}, " +
                   $"{nameof(DeltaAdjEdge)}, " +
                   $"{nameof(BuyEdgeToTheo)}, " +
                   $"{nameof(SellEdgeToTheo)}, " +
                   $"{nameof(BuyFirst)}, " +
                   $"{nameof(SellFirst)}, " +
                   $"{nameof(BuyTime)}, " +
                   $"{nameof(SellTime)}, " +
                   $"{nameof(BuySymbol)}, " +
                   $"{nameof(SellSymbol)}, " +
                   $"{nameof(NearExpiration)}, " +
                   $"{nameof(FarExpiration)}, " +
                   $"{nameof(Mleg)}, " +
                   $"{nameof(Latency)}, " +
                   $"{nameof(FinderLatency)}, " +
                   $"{nameof(EdgeScannerType)}, " +
                   $"{nameof(FlipCount)}, " +
                   $"{nameof(SpreadWidth)}, " +
                   $"{nameof(AbsDelta)}, " +
                   $"{nameof(BuyTradeBid)}, " +
                   $"{nameof(BuyTradeMid)}, " +
                   $"{nameof(BuyTradeAsk)}, " +
                   $"{nameof(BuyTradeTheo)}, " +
                   $"{nameof(BuyTradeDelta)}, " +
                   $"{nameof(BuyTradeUnderlyingMid)}, " +
                   $"{nameof(SellTradeUnderlyingMid)}, " +
                   $"{nameof(SellTradeBid)}, " +
                   $"{nameof(SellTradeMid)}, " +
                   $"{nameof(SellTradeAsk)}, " +
                   $"{nameof(SellTradeTheo)}, " +
                   $"{nameof(SellTradeDelta)}, " +
                   $"{nameof(BuyBidPercent)}, " +
                   $"{nameof(SellBidPercent)}, " +
                   $"{nameof(BuyConditionCode)}, " +
                   $"{nameof(SellConditionCode)}, " +
                   $"{nameof(BuyUnderlyingWidth)}, " +
                   $"{nameof(SellUnderlyingWidth)}, " +
                   $"{nameof(Traded)}, " +
                   $"{nameof(Message)}, " +
                   $"{nameof(LegsCount)}, " +
                   $"{nameof(Condition)}, \n";
        }

        private string GetConditionCode(char conditionCode)
        {
            return conditionCode switch
            {
                'a' => "SingLegAuct",
                'b' => "SingLegAuctISO",
                'c' => "SingLegCross",
                'd' => "SingLegCrossISO",
                'e' => "SingLegFloor",
                'f' => "MultLegAutoEx",
                'g' => "MultLegAuct",
                'h' => "MultLegCross",
                'i' => "MultLegFlr",
                'j' => "MultAutoSingLeg",
                'k' => "MultStkOptAuct",
                'l' => "MultLegAuctSingLeg",
                'm' => "MultLegFlrSingLeg",
                'n' => "MultStkOptAutoEx",
                'o' => "MultStkOptCrossAutoEx",
                'p' => "MultStkOptFlr",
                'q' => "MultStkOptAutoSingLeg",
                'r' => "MultStkOptAuctSingLeg",
                's' => "MultStkOptFlrSingLeg",
                't' => "MultLegFlrPropProd",
                'u' => "MultComprPropProd",
                'v' => "ExtendedHrs",
                'A' => "Canceled",
                'B' => "LateOutOfSeq",
                'C' => "CanceledLast",
                'D' => "Late",
                'E' => "CanceledOpen",
                'F' => "OpenLate",
                'G' => "CanceledOnly",
                'H' => "OpenLast",
                'I' => "Auto",
                'J' => "Reopen",
                'S' => "ISOI",
                _ => " ",
            };
        }

        public SignalKey GetKey()
        {
            var key = new SignalKey(
                SpreadId,
                EdgeScannerType,
                BuyTime,
                SellTime,
                BuyPrice,
                SellPrice,
                BuyQty,
                SellQty,
                BuyConditionCode,
                SellConditionCode
            );
            return key;
        }
    }

    public record struct SignalKey(
        string SpreadId,
        EdgeScannerType EdgeScannerType,
        DateTime BuyTime,
        DateTime SellTime,
        double BuyPrice,
        double SellPrice,
        int BuyQty,
        int SellQty,
        char BuyConditionCode,
        char SellConditionCode
    );

    public partial class EdgeScanStatLogger : BindableBase
    {

        [Bindable(Default = double.NaN)]
        public partial double MidAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAtEntry { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAt1Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAt5Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAt10Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAt30Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAt60Minute { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double MidAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double PnlAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double GammaAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double VegaAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderlyingAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ImpliedAtClose { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double DeltaAdjTheoAtClose { get; set; }
    }
}
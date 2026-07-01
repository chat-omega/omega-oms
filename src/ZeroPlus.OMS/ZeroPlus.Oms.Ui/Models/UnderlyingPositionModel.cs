using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.TagCodecLib;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class UnderlyingPositionModel : ViewModelBase, IOmsDataSubscriber, IOmsOrderUpdateSubscriber, IEmaConfig, IOmsPositionSubscriber
    {

        private readonly ConcurrentStack<TradeUnit> _sells = new();
        private readonly ConcurrentStack<TradeUnit> _buys = new();

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly object _hedgeLock = new();
        private readonly EmaCalculator _emaCalculator;
        private readonly EmaCalculator _pnlEmaCalculator;
        private readonly Timer _checkTimer;
        internal ConcurrentDictionary<string, HedgePositionModel> _symbolToPositionModelMap = new();
        internal double _ask = double.NaN;
        internal double _bid = double.NaN;
        private DateTime _lastHedgeTime;
        private double _lastHedgeMid = double.NaN;
        private double _underlyingClosing = double.NaN;
        private bool _underlyingClosingInitialized;
        private string _hedgeOrderId;
        private readonly HashSet<string> _hedgeOrderIdsSet;
        private readonly HashSet<string> _buyHedgeOrderIdsSet;
        private readonly HashSet<string> _sellHedgeOrderIdsSet;
        private readonly HashSet<string> _restingHedgeOrderIdsSet;
        private readonly HashSet<string> _manualHedgeOrderIdsSet;
        private Side? _lastSide;
        private bool _firstHedgeSet;
        internal bool CanHedge = true;
        private readonly MDUnderlying _underlyingDetails;
        private string _restingUpOrderId;
        private string _restingDownOrderId;
        private double _restingUpCancelTrigger;
        private double _restingDownCancelTrigger;


        public event ResetEmaEventHandler ResetEmaEvent;
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public ObservableCollection<RiskProfileModel> ScalpedPositionsRisk { get; set; }
        public bool IsDisposed { get; set; }
        public OrderSubType? SubType { get; set; } = ZeroPlus.Models.Data.Enums.OrderSubType.DeltaHedge;
        public IDeltaHedgeManagerModel DeltaHedgeManagerModel { get; }
        public EmaConfig PnlEmaConfig { get; } = new EmaConfig();
        [Bindable]
        public partial bool Active { get; set; }
        [Bindable]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [Bindable]
        public partial ComplexOrderTicketViewModel OrderTicket { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string HedgeSymbol { get; set; }
        [Bindable]
        public partial double HedgeMultiplier { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Last { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Mid { get; set; }
        [Bindable]
        public partial int BuyQty { get; set; }
        [Bindable]
        public partial int SellQty { get; set; }
        [Bindable]
        public partial double AvgBuy { get; set; }
        [Bindable]
        public partial double AvgSell { get; set; }
        [Bindable]
        public partial bool DoNotCrossLastFill { get; set; }
        [Bindable]
        public partial bool RestOrdersEnabled { get; set; }
        [Bindable]
        public partial double RestingProximityTrigger { get; set; }
        [Bindable]
        public partial double RestingCancelTrigger { get; set; }
        [Bindable]
        public partial double RestingCancelDelay { get; set; }
        [Bindable]
        public partial double LiveEmaExtension { get; set; }
        [Bindable]
        public partial double NetChange { get; set; }
        [Bindable]
        public partial string NetPosition { get; set; }
        [Bindable]
        public partial int HedgedQty { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial double NetGamma { get; set; }
        [Bindable]
        public partial double NetTheta { get; set; }
        [Bindable]
        public partial double IvVegaPnl { get; set; }
        [Bindable]
        public partial double ThetaPnl { get; set; }
        [Bindable]
        public partial int HedgeQty { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial string StatusSell { get; set; }
        [Bindable]
        public partial StatusMode StatusModeSell { get; set; }
        [Bindable]
        public partial int FilledQty { get; set; }
        [Bindable]
        public partial int WorkingQty { get; set; }
        [Bindable]
        public partial string Route { get; set; }
        [Bindable]
        public partial double MinEdge { get; set; }
        [Bindable]
        public partial double MinHedgeInterval { get; set; }
        [Bindable]
        public partial double HedgePercent { get; set; }
        [Bindable]
        public partial double MinScalpTrigger { get; set; }
        [Bindable]
        public partial double TargetDelta { get; set; }
        [Bindable]
        public partial double ScalpPnl { get; set; }
        [Bindable]
        public partial double ScalpUnrealPnl { get; set; }
        [Bindable]
        public partial double PositionUnrealPnl { get; set; }
        [Bindable]
        public partial double PositionRealPnl { get; set; }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial bool IsCancelEnabled { get; set; }
        [Bindable]
        public partial bool IsSubmitEnabled { get; set; }
        [Bindable]
        public partial GammaScalpTriggerMode Mode { get; set; }
        [Bindable]
        public partial string InstanceId { get; set; }
        [Bindable]
        public partial bool EmaEnabled { get; set; }
        [Bindable]
        public partial EmaType SelectedEmaType { get; set; }
        [Bindable]
        public partial double PercentVegaThreshold { get; set; }
        [Bindable]
        public partial double EmaSmoothing { get; set; }
        [Bindable]
        public partial double EmaPeriods { get; set; }
        [Bindable]
        public partial double EmaInterval { get; set; }
        [Bindable]
        public partial double MaxBidDeviation { get; set; }
        [Bindable]
        public partial double MaxAskDeviation { get; set; }
        [Bindable]
        public partial double Ema { get; set; }
        [Bindable]
        public partial double PnlEma { get; set; }
        [Bindable]
        public partial double EmaExtension { get; set; }
        [Bindable]
        public partial double EmaOffset { get; set; }
        [Bindable]
        public partial double DeltaOffset { get; set; }
        [Bindable]
        public partial double OverhedgeMaxPercent { get; set; }
        [Bindable]
        public partial double OverhedgeMaxShare { get; set; }
        [Bindable]
        public partial int MinQty { get; set; }

        public UnderlyingPositionModel(IDeltaHedgeManagerModel managerModel, string underlyingSymbol, string hedgeUnderlying = null, double hedgeMultiplier = 1, MDUnderlying details = null)
        {
            DeltaHedgeManagerModel = managerModel;
            _emaCalculator = new EmaCalculator(this, SubscriptionFieldType.MidPoint);
            _pnlEmaCalculator = new EmaCalculator(PnlEmaConfig, SubscriptionFieldType.MidPoint);
            _hedgeOrderIdsSet = new HashSet<string>();
            _buyHedgeOrderIdsSet = new HashSet<string>();
            _sellHedgeOrderIdsSet = new HashSet<string>();
            _restingHedgeOrderIdsSet = new HashSet<string>();
            _manualHedgeOrderIdsSet = new HashSet<string>();
            _underlyingDetails = details ?? OmsCore.QuoteClient.GetUnderlyingDetails(Symbol);
            Symbol = underlyingSymbol;
            HedgeSymbol = hedgeUnderlying ?? underlyingSymbol;
            HedgeMultiplier = hedgeMultiplier;
            MinScalpTrigger = 0.1;
            RestingProximityTrigger = 0.2;
            RestingCancelTrigger = 0.1;
            MinHedgeInterval = 500;
            HedgePercent = 0;
            Active = false;

            if (DeltaHedgeManagerModel.GammaScalper)
            {
                Mode = GammaScalpTriggerMode.Ema;
                HedgePercent = 1;
                MinEdge = 0.08;
                EmaEnabled = true;
                EmaSmoothing = 2;
                EmaInterval = 60000;
                EmaPeriods = 10;
                EmaExtension = 0.10;
                DoNotCrossLastFill = true;
                MinQty = 5;
                _emaCalculator.EmaUpdatedEvent += OnEmaCalculatorEmaUpdatedEvent;
                _pnlEmaCalculator.EmaUpdatedEvent += OnPnlEmaCalculatorEmaUpdatedEvent;
                InstanceId = Guid.NewGuid().ToString().Split('-')[0];
                SubType = ZeroPlus.Models.Data.Enums.OrderSubType.GammaScalp;
                ScalpedPositionsRisk = new ObservableCollection<RiskProfileModel>();
                for (int i = 0; i < 11; i++)
                {
                    RiskProfileModel riskPosition = new();
                    if (i == 5)
                    {
                        riskPosition.IsCurrent = true;
                    }
                    ScalpedPositionsRisk.Add(riskPosition);
                }

                _checkTimer = new Timer(500)
                {
                    AutoReset = false,
                };
                _checkTimer.Elapsed += CheckTimer_Elapsed;
                _checkTimer.Start();
            }
            OmsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.LastPrice, this);
            OmsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.Bid, this);
            OmsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.Ask, this);
            Route = OmsCore.Config.DefaultHedgeRoute(OmsCore.Config.InstanceModeV3);
            OmsCore.QuoteClient.GetSnapshotAsync(HedgeSymbol, SubscriptionFieldType.PreviousClose)
               .ContinueWith(t =>
               {
                   _underlyingClosing = t.Result;
                   _underlyingClosingInitialized = true;
               });
        }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                CheckForTrade();
            }
            finally
            {
                _checkTimer.Start();
            }
        }

        private void OnEmaCalculatorEmaUpdatedEvent(double ema)
        {
            Ema = EmaOffset + ema;
            LiveEmaExtension = _mid - ema;
        }

        private void OnPnlEmaCalculatorEmaUpdatedEvent(double ema)
        {
            PnlEma = ema;
        }

        [Command]
        public void ResetEmaCommand()
        {
            try
            {
                ResetEmaEvent?.Invoke();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetEmaCommand));
            }
        }

        [Command]
        public void ResetPnlEmaCommand()
        {
            try
            {
                PnlEmaConfig?.ResetEma();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetPnlEmaCommand));
            }
        }

        [Command]
        public void ActivateAllCommand()
        {
            try
            {
                for (int i = 0; i < OrderTicket.Legs.Count; i++)
                {
                    TicketLegModel position = OrderTicket.Legs[i];
                    position.Active = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void DeactivateAllCommand()
        {
            try
            {
                for (int i = 0; i < OrderTicket.Legs.Count; i++)
                {
                    TicketLegModel position = OrderTicket.Legs[i];
                    position.Active = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void OpenInPositionManagerCommand(HedgePositionModel hedgePositionModel)
        {
            HedgePositionManagementView managementView = new();
            if (managementView.DataContext is HedgePositionManagementViewModel positionManagementViewModel)
            {
                positionManagementViewModel.UnderlyingPositionModel = this;
                positionManagementViewModel.SelectedPosition = hedgePositionModel;
                managementView.Show();
            }
            else
            {
                _log.Error(nameof(OpenInPositionManagerCommand) + " position manager load failed.");
            }
        }

        [Command]
        public void SubmitAsyncCommand()
        {
            try
            {
                HedgeWithStock(Mid, HedgeQty, InstanceId, forced: true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitAsyncCommand));
            }
        }

        [Command]
        public void CancelAsyncCommand()
        {
            OmsCore.OrderClient.CancelOrder(new CancelRequest
            {
                OrderId = _hedgeOrderId,
                Venue = OrderTicket.Venue,
                LocalId = OrderTicket.LocalID,
                PermId = OrderTicket.PermID,
                Account = OrderTicket.Account,
                UserId = OrderTicket.UserId,
                RiskCheckId = OrderTicket.RiskCheckId
            });
        }

        [Command]
        public void ResetAvgPxCommand()
        {
            AvgBuy = 0;
            AvgSell = 0;
            BuyQty = 0;
            SellQty = 0;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            SubscriptionFieldType type = key.Type;
            if (value is double update)
            {
                switch (type)
                {
                    case SubscriptionFieldType.LastPrice:
                        _last = update;
                        Last = _last;
                        if (_underlyingClosingInitialized)
                        {
                            NetChange = _last - _underlyingClosing;
                        }
                        break;
                    case SubscriptionFieldType.Bid:
                        _bid = update;
                        UpdateMid();
                        break;
                    case SubscriptionFieldType.Ask:
                        _ask = update;
                        UpdateMid();
                        break;
                }
            }
        }

        public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
        {
            CheckForTrade();
        }

        private void UpdateMid()
        {
            _mid = (_bid + _ask) / 2;
            Mid = _mid;
            LiveEmaExtension = _mid - Ema;
            CheckRestingOrdersForCancel();
            CheckForTrade();
            if (DeltaHedgeManagerModel.GammaScalper)
            {
                _emaCalculator.AddUpdate(_mid);
                UpdateRisk(_mid);
            }
            UpdateNetPnl();
        }

        private void CheckRestingOrdersForCancel()
        {
            bool allFlat = OrderTicket == null || OrderTicket.Legs.All(x => x.NetQty == 0);
            if (!string.IsNullOrWhiteSpace(_restingUpOrderId))
            {
                if (_mid < _restingUpCancelTrigger || allFlat)
                {
                    OmsCore.OrderClient.CancelOrder(new CancelRequest
                    {
                        OrderId = _restingUpOrderId,
                        Venue = OrderTicket.Venue,
                        LocalId = OrderTicket.LocalID,
                        PermId = OrderTicket.PermID,
                        Account = OrderTicket.Account,
                        UserId = OrderTicket.UserId,
                        RiskCheckId = OrderTicket.RiskCheckId
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(_restingDownOrderId))
            {
                if (_mid > _restingDownCancelTrigger || allFlat)
                {
                    OmsCore.OrderClient.CancelOrder(new CancelRequest
                    {
                        OrderId = _restingDownOrderId,
                        Venue = OrderTicket.Venue,
                        LocalId = OrderTicket.LocalId,
                        PermId = OrderTicket.PermID,
                        Account = OrderTicket.Account,
                        UserId = OrderTicket.UserId,
                        RiskCheckId = OrderTicket.RiskCheckId
                    });
                }
            }
        }

        private void CheckForTrade()
        {
            if (DeltaHedgeManagerModel.GammaScalper && OrderTicket != null)
            {
                bool allLoaded = !OrderTicket.Legs.Any(x => x.NetQty == 0);
                bool allFlat = OrderTicket.Legs.All(x => x.NetQty == 0);
                switch (Mode)
                {
                    case GammaScalpTriggerMode.Delta:
                        Update(DeltaHedgeManagerModel.RoundDeltaForHedge, DeltaHedgeManagerModel.GammaScalper, DeltaHedgeManagerModel.IsHedging);
                        break;
                    case GammaScalpTriggerMode.Mid when !double.IsNaN(_mid):
                        if ((allLoaded && FilledQty == 0 && WorkingQty == 0 && DeltaHedgeManagerModel.IsHedging) ||
                            (allFlat && FilledQty != 0))
                        {
                            Update(DeltaHedgeManagerModel.RoundDeltaForHedge, DeltaHedgeManagerModel.GammaScalper, DeltaHedgeManagerModel.IsHedging);
                        }
                        else if (Math.Abs(_mid - _lastHedgeMid) >= MinScalpTrigger)
                        {
                            Update(DeltaHedgeManagerModel.RoundDeltaForHedge, DeltaHedgeManagerModel.GammaScalper);
                        }
                        break;
                    case GammaScalpTriggerMode.Ema when !double.IsNaN(Ema) && !double.IsNaN(_mid):
                        if ((allLoaded && FilledQty == 0 && WorkingQty == 0 && DeltaHedgeManagerModel.IsHedging) ||
                            (allFlat && FilledQty != 0))
                        {
                            Update(DeltaHedgeManagerModel.RoundDeltaForHedge, DeltaHedgeManagerModel.GammaScalper, DeltaHedgeManagerModel.IsHedging);
                        }
                        else if (_mid > Ema + EmaExtension ||
                                 _mid < Ema - EmaExtension)
                        {
                            Update(DeltaHedgeManagerModel.RoundDeltaForHedge, DeltaHedgeManagerModel.GammaScalper);
                        }
                        break;
                }
                if (RestOrdersEnabled)
                {
                    lock (_hedgeLock)
                    {
                        double midUp = _mid + RestingProximityTrigger;
                        double midDown = _mid - RestingProximityTrigger;

                        double netDelta = DeltaOffset;
                        double netDeltaDown = DeltaOffset;

                        for (int i = 0; i < OrderTicket.Legs.Count; i++)
                        {
                            TicketLegModel position = OrderTicket.Legs[i];
                            if (position.Active)
                            {
                                Greeks greeks = position.UpdateGreeks(_underlyingDetails, midUp);
                                netDelta += greeks.Delta * position.ActualQty * position.Multiplier * HedgeMultiplier;
                                Greeks greeksDown = position.UpdateGreeks(_underlyingDetails, midDown);
                                netDeltaDown += greeksDown.Delta * position.ActualQty * position.Multiplier * HedgeMultiplier;
                            }
                        }

                        if (!double.IsNaN(netDelta))
                        {
                            int hedgeQty = (int)Math.Round(netDelta * -1 * HedgePercent) - FilledQty;

                            if (Active && hedgeQty != 0 && Math.Abs(hedgeQty) >= MinQty && DeltaHedgeManagerModel.IsHedging && string.IsNullOrEmpty(_restingUpOrderId))
                            {
                                _restingUpCancelTrigger = _mid - RestingCancelTrigger;
                                HedgeWithStock(midUp, hedgeQty, InstanceId, forced: true, RestingProximityTrigger, restingUp: true);
                            }
                        }

                        if (!double.IsNaN(netDeltaDown))
                        {
                            int hedgeQty = (int)Math.Round(netDeltaDown * -1 * HedgePercent) - FilledQty;

                            if (Active && hedgeQty != 0 && Math.Abs(hedgeQty) >= MinQty && DeltaHedgeManagerModel.IsHedging && string.IsNullOrEmpty(_restingDownOrderId))
                            {
                                _restingDownCancelTrigger = _mid + RestingCancelTrigger;
                                HedgeWithStock(midDown, hedgeQty, InstanceId, forced: true, RestingProximityTrigger, restingUp: false);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateRisk(double mid)
        {
            if (OrderTicket == null)
            {
                return;
            }
            int count = ScalpedPositionsRisk.Count;
            Dictionary<int, int> map = new()
            {
                [0] = 5,
                [1] = 4,
                [2] = 3,
                [3] = 2,
                [4] = 1,
                [5] = 0,
                [6] = -1,
                [7] = -2,
                [8] = -3,
                [9] = -4,
                [10] = -5
            };

            for (int i = 0; i < count; i++)
            {
                double newMid = mid + map[i];

                double netDelta = 0.0;
                double netGamma = 0.0;
                double netTheta = 0.0;

                double qtyNetDelta = 0.0;
                double qtyNetGamma = 0.0;
                double qtyNetTheta = 0.0;

                for (int j = 0; j < OrderTicket.Legs.Count; j++)
                {
                    TicketLegModel position = OrderTicket.Legs[j];

                    if (position.Active)
                    {
                        int side = position.Side == Side.Buy ? 1 : -1;
                        Greeks greek = position.GetGreeks(_underlyingDetails, newMid);

                        netDelta += greek.Delta * position.ActualQty * position.Multiplier * HedgeMultiplier;
                        netGamma += greek.Gamma * position.ActualQty * position.Multiplier;
                        netTheta += greek.Theta * position.ActualQty * position.Multiplier;

                        qtyNetDelta += greek.Delta * position.Quantity * side * position.Multiplier * HedgeMultiplier;
                        qtyNetGamma += greek.Gamma * position.Quantity * side * position.Multiplier;
                        qtyNetTheta += greek.Theta * position.Quantity * side * position.Multiplier;
                    }
                }

                netDelta += DeltaOffset;
                qtyNetDelta += DeltaOffset;

                ScalpedPositionsRisk[i].UnderlyingPrice = newMid;

                ScalpedPositionsRisk[i].NetDelta = netDelta;
                ScalpedPositionsRisk[i].NetGamma = netGamma;
                ScalpedPositionsRisk[i].NetTheta = netTheta;

                ScalpedPositionsRisk[i].QtyNetDelta = qtyNetDelta;
                ScalpedPositionsRisk[i].QtyNetGamma = qtyNetGamma;
                ScalpedPositionsRisk[i].QtyNetTheta = qtyNetTheta;
            }
        }

        private void UpdateDelta()
        {
            if (DeltaHedgeManagerModel.GammaScalper && OrderTicket != null)
            {
                for (int i = 0; i < OrderTicket.Legs.Count; i++)
                {
                    TicketLegModel position = OrderTicket.Legs[i];
                    position.UpdateGreeks(_underlyingDetails, _mid);
                }
            }
        }

        private void UpdateNetPnl()
        {
            if (OrderTicket == null)
            {
                return;
            }
            while (!_buys.IsEmpty && !_sells.IsEmpty)
            {
                if (_sells.TryPop(out TradeUnit sell))
                {
                    if (_buys.TryPop(out TradeUnit buy))
                    {
                        double netPnl = sell.NetPrice - buy.NetPrice;
                        ScalpPnl += netPnl;
                    }
                    else
                    {
                        _sells.Push(sell);
                    }
                }
            }

            double openPositionAveragePrice = 0.0;
            if (!_sells.IsEmpty)
            {
                openPositionAveragePrice += _sells.Sum(x => x.Price);
            }
            if (!_buys.IsEmpty)
            {
                openPositionAveragePrice -= _buys.Sum(x => x.Price);
            }
            openPositionAveragePrice = FilledQty != 0 ? Math.Abs(openPositionAveragePrice / Math.Abs(FilledQty)) : 0;

            if (FilledQty < 0)
            {
                ScalpUnrealPnl = (openPositionAveragePrice - _ask) * Math.Abs(FilledQty);
            }
            else if (FilledQty > 0)
            {
                ScalpUnrealPnl = (_bid - openPositionAveragePrice) * FilledQty;
            }
            else
            {
                ScalpUnrealPnl = 0;
            }

            double positionUnrealPnl = 0.0;
            double positionRealPnl = 0.0;
            for (int i = 0; i < OrderTicket.Legs.Count; i++)
            {
                TicketLegModel position = OrderTicket.Legs[i];
                if (position.AddToPnl)
                {
                    positionUnrealPnl += position.UnrealPnl;
                    positionRealPnl += position.RealizedPL;
                }
            }

            PositionUnrealPnl = positionUnrealPnl;
            PositionRealPnl = positionRealPnl;

            NetPnl = ScalpPnl + ScalpUnrealPnl + PositionUnrealPnl + PositionRealPnl;

            _pnlEmaCalculator.AddUpdate(NetPnl);
        }

        internal void AddPosition(HedgePositionModel model, bool overrideExisting = true)
        {
            if (!_symbolToPositionModelMap.ContainsKey(model.Symbol))
            {
                model.Active = true;
                _symbolToPositionModelMap[model.Symbol] = model;
                if (DeltaHedgeManagerModel.GammaScalper)
                {
                    OmsCore.OrderClient.SubscribePosition(model.Symbol, DeltaHedgeManagerModel.Account, this);
                }
            }
            else if (overrideExisting)
            {
                if (_symbolToPositionModelMap.TryRemove(model.Symbol, out HedgePositionModel oldModel))
                {
                    model.Position.QtyOffSet = oldModel.Position.QtyOffSet;
                    oldModel.Position = model.Position;
                }
            }
            UpdateDelta();
        }

        internal void Update(bool roundDeltas, bool hedgeAfterUpdate = false, bool forced = false)
        {
            try
            {
                double ivVegaPnl = 0.0;
                double thetaPnl = 0.0;
                double netDelta = 0.0;
                double netGamma = 0.0;
                double netTheta = 0.0;
                int undefined = 0;
                int calls = 0;
                int puts = 0;
                double mid = _mid;
                lock (_hedgeLock)
                {
                    for (int i = 0; i < OrderTicket.Legs.Count; i++)
                    {
                        TicketLegModel position = OrderTicket.Legs[i];
                        if (position.Active)
                        {
                            Greeks greeks = position.UpdateGreeks(_underlyingDetails, mid);
                            netDelta += greeks.Delta * position.ActualQty * position.Multiplier * HedgeMultiplier;
                            netGamma += greeks.Gamma * position.ActualQty * position.Multiplier;
                            netTheta += greeks.Theta * position.ActualQty * position.Multiplier;
                            thetaPnl += greeks.Theta / 24 * (DateTime.Now - position.PutOnTime).TotalHours * position.ActualQty * position.Multiplier;
                            ivVegaPnl += position.IvVegaPnl;
                            if (!DeltaHedgeManagerModel.GammaScalper)
                            {
                                switch (position.Type)
                                {
                                    case "STOCK":
                                        undefined += position.ActualQty;
                                        break;
                                    case "PUT":
                                        puts += position.ActualQty;
                                        break;
                                    case "CALL":
                                        calls += position.ActualQty;
                                        break;
                                }
                            }
                        }
                    }
                }

                netDelta += DeltaOffset;
                NetDelta = netDelta;
                NetGamma = netGamma;
                NetTheta = netTheta;
                IvVegaPnl = ivVegaPnl;
                ThetaPnl = thetaPnl;
                NetPosition = $"U: {undefined} | C: {calls} | P: {puts}";

                if (!double.IsNaN(netDelta))
                {
                    if (roundDeltas)
                    {
                        double percentDelta = Math.Round(netDelta * HedgePercent);
                        int num = (int)(percentDelta % 100);
                        HedgeQty = ((int)(Math.Abs(num) <= 50 ? (percentDelta - num) * -1 : (percentDelta - num + (num > 0 ? 100 : -100)) * -1)) - FilledQty;
                    }
                    else
                    {
                        HedgeQty = (int)Math.Round(netDelta * -1 * HedgePercent) - FilledQty;
                    }

                    IsSubmitEnabled = Active && HedgeQty != 0 && HedgeQty - WorkingQty != 0;

                    if (((CanHedge && !DeltaHedgeManagerModel.GammaScalper) || hedgeAfterUpdate) && IsSubmitEnabled && DeltaHedgeManagerModel.IsHedging)
                    {
                        HedgeWithStock(mid, HedgeQty, InstanceId, forced);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        internal void Dispose()
        {
            try
            {
                _checkTimer?.Stop();
                OmsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.Ask, this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
        }

        public void OrderUpdated(OrderUpdateValues orderUpdate)
        {
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            OrderStatus? orderStatus = execReport.OrderStatus;
            ExecutionType? executionType = execReport.ExecutionType;

            Side hedgeSide = Side.Buy;
            bool validReport = false;
            if (execReport.Side is Side.Buy or Side.BuyToCover)
            {
                hedgeSide = Side.Buy;
                validReport = true;
            }
            else if (execReport.Side is Side.Sell or Side.SellShort)
            {
                hedgeSide = Side.Sell;
                validReport = true;
            }
            if (validReport)
            {
                if (executionType != null && executionType.Value.IsFilled())
                {
                    int hedgeFillQty = hedgeSide == Side.Sell ? -Math.Abs(execReport.LastQty) : Math.Abs(execReport.LastQty);
                    if (!_manualHedgeOrderIdsSet.Contains(execReport.ClientOrderId))
                    {
                        if (hedgeSide == Side.Sell)
                        {
                            int qty = execReport.LastQty;
                            double avgPx = execReport.AvgPrice;
                            AvgSell = ((AvgSell * SellQty) + (avgPx * qty)) / (SellQty + qty);
                            SellQty += qty;
                        }
                        else
                        {
                            int qty = execReport.LastQty;
                            double avgPx = execReport.AvgPrice;
                            AvgBuy = ((AvgBuy * BuyQty) + (avgPx * qty)) / (BuyQty + qty);
                            BuyQty += qty;
                        }
                    }

                    lock (_hedgeLock)
                    {
                        FilledQty += hedgeFillQty;
                        WorkingQty -= hedgeFillQty;
                        if (execReport.ClientOrderId == _restingUpOrderId)
                        {
                            _restingUpOrderId = "";
                        }
                        else if (execReport.ClientOrderId == _restingDownOrderId)
                        {
                            _restingDownOrderId = "";
                        }
                    }
                    CanHedge = false;
                    bool isFlat = HedgeQty - WorkingQty == 0;
                    IsSubmitEnabled = Active && !isFlat;

                    if (!_firstHedgeSet)
                    {
                        _firstHedgeSet = true;
                        AvgBuy = 0;
                        BuyQty = 0;
                        AvgSell = 0;
                        SellQty = 0;
                    }
                    if (_lastSide.HasValue && _lastSide != hedgeSide)
                    {
                        if (hedgeSide == Side.Sell)
                        {
                            AvgBuy = 0;
                            BuyQty = 0;
                        }
                        else
                        {
                            AvgSell = 0;
                            SellQty = 0;
                        }
                    }
                    _lastSide = hedgeSide;

                    TradeUnit singleTrade = new()
                    {
                        Quantity = 1,
                        Price = execReport.AvgPrice,
                        TotalPrice = execReport.AvgPrice,
                        NetPrice = execReport.AvgPrice,
                    };
                    for (int i = 0; i < execReport.LastQty; i++)
                    {
                        if (hedgeSide == Side.Sell)
                        {
                            _sells.Push(singleTrade);
                        }
                        else
                        {
                            _buys.Push(singleTrade);
                        }
                    }

                    UpdateNetPnl();
                }
                else if (orderStatus is OrderStatus.Canceled or
                         OrderStatus.Rejected)
                {
                    int hedgeQty = execReport.Qty - execReport.CumQty;
                    hedgeQty = hedgeSide == Side.Sell ? -Math.Abs(hedgeQty) : Math.Abs(hedgeQty);
                    lock (_hedgeLock)
                    {
                        WorkingQty -= hedgeQty;
                        CanHedge = execReport.CumQty == 0;
                        IsSubmitEnabled = Active && HedgeQty - WorkingQty != 0;
                        if (execReport.ClientOrderId == _restingUpOrderId)
                        {
                            _restingUpOrderId = "";
                        }
                        else if (execReport.ClientOrderId == _restingDownOrderId)
                        {
                            _restingDownOrderId = "";
                        }
                        if (hedgeQty != 0)
                        {
                            CheckForTrade();
                        }
                    }
                }
            }

            ParseOrderUpdate(execReport);
        }

        public void AutomationStateChanged(bool running)
        {
        }

        private void ParseOrderUpdate(OrderUpdateModel execReport)
        {
            int inverter = 1;


            bool isBuySide = _buyHedgeOrderIdsSet.Contains(execReport.ClientOrderId);
            if (isBuySide)
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        Status = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Reset;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.PendingNew:
                        Status = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Pending;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.PartiallyFilled:
                        Status = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusMode = StatusMode.NewSell;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.Filled:
                        Status = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusMode = StatusMode.FilledSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Canceled:
                        Status = execReport.CumQty == 0 && execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        StatusMode = StatusMode.CancelledSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Rejected:
                        Status = $"Rejected {execReport.Message}";
                        StatusMode = StatusMode.RejectedSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Replaced:
                        Status = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Reset;
                        IsCancelEnabled = false;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    Status = $"Cancel Rejected {execReport.Message}";
                    StatusMode = StatusMode.RejectedBuy;

                    if (execReport.ClientOrderId == _restingUpOrderId)
                    {
                        _restingUpOrderId = "";
                    }
                    else if (execReport.ClientOrderId == _restingDownOrderId)
                    {
                        _restingDownOrderId = "";
                    }
                }
            }
            else
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        StatusSell = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Reset;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.PendingNew:
                        StatusSell = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Pending;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.PartiallyFilled:
                        StatusSell = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusModeSell = StatusMode.NewSell;
                        IsCancelEnabled = true;
                        break;
                    case OrderStatus.Filled:
                        StatusSell = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusModeSell = StatusMode.FilledSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Canceled:
                        StatusSell = execReport.CumQty == 0 && execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        StatusModeSell = StatusMode.CancelledSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Rejected:
                        StatusSell = $"Rejected {execReport.Message}";
                        StatusModeSell = StatusMode.RejectedSell;
                        IsCancelEnabled = false;
                        break;
                    case OrderStatus.Replaced:
                        StatusSell = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Reset;
                        IsCancelEnabled = false;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    StatusSell = $"Cancel Rejected {execReport.Message}";
                    StatusModeSell = StatusMode.RejectedSell;
                    if (execReport.ClientOrderId == _restingUpOrderId)
                    {
                        _restingUpOrderId = "";
                    }
                    else if (execReport.ClientOrderId == _restingDownOrderId)
                    {
                        _restingDownOrderId = "";
                    }
                }
            }
        }

        public void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            Status = $"Cancel Rejected {orderCancelReject.Comment}";
            StatusMode = StatusMode.RejectedBuy;
        }

        private void HedgeWithStock(double mid, int hedgeQty, string comment = null, bool forced = false, double pxOffset = 0, bool restingUp = false)
        {
            try
            {
                lock (_hedgeLock)
                {
                    int stockHedgeQty = hedgeQty - WorkingQty;

                    if (!forced &&
                        DeltaHedgeManagerModel.GammaScalper &&
                        Mode == GammaScalpTriggerMode.Ema &&
                        !double.IsNaN(Ema) &&
                        !double.IsNaN(mid))
                    {
                        if (mid > Ema + EmaExtension && stockHedgeQty > 0)
                        {
                            return;
                        }
                        if (mid < Ema - EmaExtension && stockHedgeQty < 0)
                        {
                            return;
                        }
                    }

                    if (stockHedgeQty != 0 && Math.Abs(stockHedgeQty) >= MinQty)
                    {
                        var orderInfo = BuildStockHedgeOrderAsync(stockHedgeQty, comment, forceLean: false, pxOffset);
                        if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
                        {
                            if (double.IsNaN(orderInfo.Price))
                            {
                                Status = "[Risk] Hedge price could not be determined.";
                                StatusMode = StatusMode.NewSell;
                                return;
                            }
                            if (orderInfo.Price > OmsCore.Config.MaxAutoHedgeNetCash)
                            {
                                Status = "[Risk] Hedge price above risk limit.";
                                StatusMode = StatusMode.NewSell;
                                return;
                            }
                        }

                        if (OmsCore.Config.MaxAutoHedgePositionEnabled)
                        {
                            if (orderInfo.Qty > OmsCore.Config.MaxAutoHedgePosition)
                            {
                                Status = "[Risk] Hedge qty above risk limit.";
                                StatusMode = StatusMode.NewSell;
                                return;
                            }
                        }

                        if (!forced && Mode != GammaScalpTriggerMode.Delta && DoNotCrossLastFill)
                        {
                            if (stockHedgeQty < 0 && AvgBuy != 0 && orderInfo.Price < AvgBuy + MinEdge)
                            {
                                Status = "Price crosses last buy px.";
                                StatusMode = StatusMode.CancelledSell;
                                return;
                            }

                            if (stockHedgeQty > 0 && AvgSell != 0 && orderInfo.Price > AvgSell - MinEdge)
                            {
                                Status = "Price crosses last sell px.";
                                StatusMode = StatusMode.CancelledSell;
                                return;
                            }
                        }

                        if ((DateTime.Now - _lastHedgeTime).TotalMilliseconds < MinHedgeInterval)
                        {
                            Status = "Min Hedge Interval Passed.";
                            StatusMode = StatusMode.CancelledSell;
                            return;
                        }

                        _lastHedgeTime = DateTime.Now;
                        _lastHedgeMid = mid;
                        _hedgeOrderId = OmsCore.OrderClient.SendOrder(orderInfo, OrderTicket.GetInstanceMode(), this, false, 1);
                        WorkingQty += stockHedgeQty;
                        _hedgeOrderIdsSet.Add(_hedgeOrderId);
                        if (orderInfo.OMSSide == Side.Buy.ToString().ToUpper())
                        {
                            _buyHedgeOrderIdsSet.Add(_hedgeOrderId);
                        }
                        else
                        {
                            _sellHedgeOrderIdsSet.Add(_hedgeOrderId);
                        }
                        if (pxOffset != 0)
                        {
                            _restingHedgeOrderIdsSet.Add(_hedgeOrderId);
                            if (restingUp)
                            {
                                _restingUpOrderId = _hedgeOrderId;
                            }
                            else
                            {
                                _restingDownOrderId = _hedgeOrderId;
                            }
                        }
                    }
                }
            }
            catch (SlimException ae)
            {
                Status = ae.Message;
                StatusMode = StatusMode.NewSell;
            }
        }

        internal OpsOrderModel BuildStockHedgeOrderAsync(int qty, string comment = null, bool forceLean = false, double pxOffset = 0)
        {
            Side side = qty < 0 ? Side.Sell : Side.Buy;
            double cancelDelay = OmsCore.Config.HedgeInterval;
            var subtype = SubType;
            double price = double.NaN;
            double bid = _bid;
            double ask = _ask;
            string type = DeltaHedgeManagerModel.OrderType.ToString().ToUpper();
            if (pxOffset != 0)
            {
                cancelDelay = RestingCancelDelay;
                type = "LIMIT";
                price = side == Side.Buy ? bid - pxOffset : ask + pxOffset;
            }
            else if (forceLean && DeltaHedgeManagerModel.OrderType != ZeroPlus.Models.Data.Enums.OrderType.Market)
            {
                price = side == Side.Buy ? ask + DeltaHedgeManagerModel.InitialHedgeLimitDiff : bid - DeltaHedgeManagerModel.InitialHedgeLimitDiff;
            }
            else
            {
                switch (DeltaHedgeManagerModel.OrderType)
                {
                    case ZeroPlus.Models.Data.Enums.OrderType.Market:
                        switch (DeltaHedgeManagerModel.LimitHandling)
                        {
                            case LimitHandling.HitBest:
                                price = side == Side.Buy ? ask : bid;
                                break;
                            case LimitHandling.Lean:
                                price = side == Side.Buy ? ask + DeltaHedgeManagerModel.AutoHedgeLimitDiff : bid - DeltaHedgeManagerModel.AutoHedgeLimitDiff;
                                break;
                            case LimitHandling.MidPoint:
                                price = (ask + bid) / 2;
                                break;
                            case LimitHandling.Last:
                                price = _last;
                                break;
                        }
                        break;
                    default:
                        price = _last;
                        break;
                }
            }

            comment ??= GeHedgeIdentifier();

            string tif = ZeroPlus.Models.Data.Enums.TimeInForce.DAY.ToString();
            if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
            {
                tif = Route.StartsWith("D") ?
                    ZeroPlus.Models.Data.Enums.TimeInForce.GTX.ToString() :
                    ZeroPlus.Models.Data.Enums.TimeInForce.ETH.ToString();
            }

            var order = new OpsOrderModel()
            {
                Symbol = HedgeSymbol,
                Qty = Math.Abs(qty),
                OMSSide = side.ToString(),
                OpenClose = "Auto",
                Price = Math.Round(price, 2),
                Account = DeltaHedgeManagerModel.Account,
                Tif = tif,
                Route = Route,
                OMSOrderType = type,
                Timestamp = DateTime.Now,
                UnderlyingSymbol = HedgeSymbol,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,
                Tag = new TagCodec(_trader: OmsCore.User.Username,
                                   _edge: pxOffset,
                                   _type: OmsCore.OrderClient.TYPE,
                                   _subtype: subtype?.ToSpacedString(),
                                   _tv: 0,
                                   _ema: 0,
                                   _bid: bid,
                                   _ask: ask,
                                   _comment: !string.IsNullOrEmpty(comment) ? comment : "").Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(comment) ? comment : "",
                    Bid = bid,
                    Ask = ask,
                    BidSize = 0,
                    AskSize = 0,
                    Theo = 0,
                    Ema = 0,
                    UnderBid = OrderTicket.UnderBid,
                    UnderAsk = OrderTicket.UnderAsk,
                    UnderBidSize = (uint)OrderTicket.UnderlyingBidSize,
                    UnderAskSize = (uint)OrderTicket.UnderlyingAskSize,
                    Edge = pxOffset,
                    OrderSubType = SubType ?? ZeroPlus.Models.Data.Enums.OrderSubType.DeltaHedge,
                    ModuleType = ZeroPlus.Models.Data.Enums.ModuleType.None,
                    VolaTheo = 0,
                    VolaTheoAdj = 0,
                    SubType = OrderTicket.SubTypeId,
                    SharedId = OrderTicket.SharedId,
                    Sequence = OrderTicket.Sequence,
                    SubTypeSequence = OrderTicket.SubTypeSequence,
                    ResubmitCount = 0,
                    TotalEstimatedResubmit = 0,
                    ParentSpreadHash = string.Empty,
                }
            };
            order.SetCancelDelay(cancelDelay);
            return order;
        }

        private string GeHedgeIdentifier()
        {
            string comment = "AUTOHEDGE - " + OmsCore.User.Username.ToUpper() + " - " + HedgeSymbol.ToUpper();
            return comment;
        }

        internal void SetOrderTicket(ComplexOrderTicketViewModel orderTicket)
        {
            OrderTicket = orderTicket;
            orderTicket.AutoCancelIntervalMin = 1100;
            orderTicket.AutoCancelIntervalMax = 1100;
            OrderTicket.TradeEvent += OnOrderEvent;
        }

        private void OnOrderEvent(OrderTicket order, Data.Trading.IOmsOrder trade)
        {
            if (order == OrderTicket)
            {
                if (trade.TradedLegs.Count > 0)
                {
                    bool isOpening = trade.TradedLegs.FirstOrDefault()?.Side == Side.Buy;
                    double netDelta = 0.0;

                    foreach (TicketLegModel leg in OrderTicket.Legs)
                    {
                        if (leg.Active)
                        {
                            Greeks greek = leg.UpdateGreeks(_underlyingDetails, _mid);
                            int side = leg.Side == Side.Buy || OrderTicket.IsSingleLeg ? 1 : -1;
                            side = isOpening ? side : -side;
                            int legQty = trade.LastQuantity * leg.Ratio * side;
                            netDelta += greek.Delta * legQty * leg.Multiplier * HedgeMultiplier;
                        }
                    }

                    if (!double.IsNaN(netDelta))
                    {
                        int stockHedgeQty = (int)Math.Round(-netDelta * HedgePercent);
                        if (DeltaHedgeManagerModel.IsHedging)
                        {
                            lock (_hedgeLock)
                            {
                                if (stockHedgeQty != 0)
                                {
                                    var orderInfo = BuildStockHedgeOrderAsync(stockHedgeQty, InstanceId, forceLean: true);

                                    if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
                                    {
                                        if (double.IsNaN(orderInfo.Price))
                                        {
                                            Status = "[Risk] Hedge price could not be determined.";
                                            StatusMode = StatusMode.NewSell;
                                        }
                                        if (orderInfo.Price > OmsCore.Config.MaxAutoHedgeNetCash)
                                        {
                                            Status = "[Risk] Hedge price above risk limit.";
                                            StatusMode = StatusMode.NewSell;
                                        }
                                    }

                                    if (OmsCore.Config.MaxAutoHedgePositionEnabled)
                                    {
                                        if (orderInfo.Qty > OmsCore.Config.MaxAutoHedgePosition)
                                        {
                                            Status = "[Risk] Hedge qty above risk limit.";
                                            StatusMode = StatusMode.NewSell;
                                        }
                                    }

                                    _hedgeOrderId = OmsCore.OrderClient.SendOrder(orderInfo, OrderTicket.GetInstanceMode(), this, false, 1);
                                    WorkingQty += stockHedgeQty;
                                    _hedgeOrderIdsSet.Add(_hedgeOrderId);
                                    _manualHedgeOrderIdsSet.Add(_hedgeOrderId);
                                    if (orderInfo.OMSSide == Side.Buy.ToString().ToUpper())
                                    {
                                        _buyHedgeOrderIdsSet.Add(_hedgeOrderId);
                                    }
                                    else
                                    {
                                        _sellHedgeOrderIdsSet.Add(_hedgeOrderId);
                                    }
                                }

                                foreach (TicketLegModel leg in OrderTicket.Legs)
                                {
                                    int side = leg.Side == Side.Buy ? 1 : -1;
                                    side = isOpening ? side : -side;
                                    int legQty = trade.LastQuantity * leg.Ratio * side;
                                    leg.ActualQty += legQty;
                                }
                                return;
                            }
                        }
                    }

                    lock (_hedgeLock)
                    {
                        foreach (TicketLegModel leg in OrderTicket.Legs)
                        {
                            int side = leg.Side == Side.Buy ? 1 : -1;
                            side = isOpening ? side : -side;
                            int legQty = trade.LastQuantity * leg.Ratio * side;
                            leg.ActualQty += legQty;
                        }
                    }
                }
            }
        }
    }
}
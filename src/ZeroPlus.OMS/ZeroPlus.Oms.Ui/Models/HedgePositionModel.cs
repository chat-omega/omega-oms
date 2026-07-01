using DevExpress.Mvvm;
using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class HedgePositionModel : BindableBase, IOmsDataSubscriber, IOmsPositionSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly IDeltaHedgeManagerModel _deltaHedgeManagerModel;
        private double _theta = double.NaN;
        private double _vega = double.NaN;
        private double _initialIv = double.NaN;
        private double _iv = double.NaN;
        private string _atmSymbol = "";
        private double _atmVega;
        private DateTime _lastWeightedVegaSetTime;


        public bool IsDisposed { get; set; }
        public PutCall OptionType { get; internal set; }
        public double Multiplier { get; private set; }
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial bool AddToPnl { get; set; }
        [Bindable]
        public partial double AvgFillPx { get; set; }
        [Bindable]
        public partial int NetQty { get; set; }
        [Bindable]
        public partial PositionSModel Position { get; set; }
        [Bindable]
        public partial bool Active { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        public Option Option { get; }
        [Bindable]
        public partial string Description { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Delta { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial double NetGamma { get; set; }
        [Bindable]
        public partial double NetTheta { get; set; }
        [Bindable]
        public partial double IvVegaPnl { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Gamma { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Bid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Ask { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Mid { get; set; }
        [Bindable]
        public partial int PostExDelta { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double NetWeightedVega { get; set; }
        [Bindable]
        public partial double GammaTheta { get; set; }

        public HedgePositionModel(Option option, IDeltaHedgeManagerModel deltaHedgeManagerModel)
        {
            _deltaHedgeManagerModel = deltaHedgeManagerModel;
            Position = new PositionSModel
            {
                Name = option.OptionSymbol
            };
            Active = false;
            AddToPnl = true;
            Symbol = option.OptionSymbol;
            Option = option;
            Subscribe(option);
        }

        public HedgePositionModel(OmsPosition position, Option option, IDeltaHedgeManagerModel deltaHedgeManagerModel)
        {
            _deltaHedgeManagerModel = deltaHedgeManagerModel;
            Position = new PositionSModel
            {
                Name = position.Symbol,
                NetQty = position.NetQty,
                OpenPositionAveragePrice = position.TradingAveCost,
            };
            Active = false;
            AddToPnl = true;
            Symbol = option.OptionSymbol;
            Option = option;
            Subscribe(option);
        }

        public HedgePositionModel(IPosition position, Option option, IDeltaHedgeManagerModel deltaHedgeManagerModel)
        {
            _deltaHedgeManagerModel = deltaHedgeManagerModel;
            Position = (PositionSModel)position;
            Active = false;
            AddToPnl = true;
            Symbol = Position.Name;
            Option = option;
            Subscribe(option);
        }

        private void Subscribe(Option option)
        {
            if (Symbol.StartsWith("."))
            {
                OptionType = option.Type switch
                {
                    Data.Securities.OptionType.PUT => PutCall.Put,
                    Data.Securities.OptionType.CALL => PutCall.Call,
                    _ => PutCall.Unknown,
                };

                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);

                Multiplier = option.Multiplier;

                if (!_deltaHedgeManagerModel.GammaScalper)
                {
                    OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Delta, this);
                    OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Gamma, this);
                    OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Theta, this);
                }
                else
                {
                    _ = SetupWeightedVegaAsync();
                }

                if (_deltaHedgeManagerModel.GammaScalper)
                {
                    OmsCore.OrderClient.SubscribePosition(Symbol, _deltaHedgeManagerModel.Account, this);
                }
            }
            else
            {
                _delta = 1;
                _theta = 0;
                Multiplier = 1;
                Delta = 1;
                OptionType = PutCall.Unknown;
            }
        }

        private async Task SetupWeightedVegaAsync()
        {
            if ((DateTime.Now - _lastWeightedVegaSetTime).TotalMilliseconds < 5000)
            {
                return;
            }
            _lastWeightedVegaSetTime = DateTime.Now;
            await Task.Run(async () =>
            {
                Option atmOption = await OptionsHelper.GetAtmOption(Option.UnderlyingSymbol, Option.Expiration, Option.Type);
                if (atmOption != null &&
                    atmOption.OptionSymbol != _atmSymbol &&
                    atmOption.Expiration == Option.Expiration)
                {
                    if (_atmSymbol != null && _atmSymbol != Symbol)
                    {
                        OmsCore.GreekClient.Unsubscribe(_atmSymbol, SubscriptionFieldType.Vega, this);
                    }
                    _atmSymbol = atmOption.OptionSymbol;
                    OmsCore.GreekClient.Subscribe(_atmSymbol, SubscriptionFieldType.Vega, this);
                }
            });
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (key.Symbol == _atmSymbol)
                {
                    if (key.Type == SubscriptionFieldType.Vega && value is double atmVega)
                    {
                        _atmVega = atmVega;
                        double weightedVega = _atmVega != 0 ? _vega / _atmVega : 0;
                        NetWeightedVega = weightedVega * Position.ActualQty * 100;
                    }
                }
                if (value is double update)
                {
                    SubscriptionFieldType type = key.Type;
                    switch (type)
                    {
                        case SubscriptionFieldType.Bid:
                            _bid = update;
                            double mid = (_bid + _ask) / 2;
                            UpdateMid(mid);
                            break;
                        case SubscriptionFieldType.Ask:
                            _ask = update;
                            mid = (_bid + _ask) / 2;
                            UpdateMid(mid);
                            break;
                        case SubscriptionFieldType.Delta:
                            _delta = update;
                            break;
                        case SubscriptionFieldType.Gamma:
                            _gamma = update;
                            break;
                        case SubscriptionFieldType.Theta:
                            _theta = update;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        private void UpdateMid(double mid)
        {
            _mid = mid;
            UpdateUnreal();
            _ = SetupWeightedVegaAsync();
        }

        private void UpdateUnreal()
        {
            if (NetQty == 0 || double.IsNaN(AvgFillPx) || double.IsNaN(_mid))
            {
                UnrealPnl = 0;
            }
            else if (NetQty < 0)
            {
                UnrealPnl = (AvgFillPx - _mid) * 100 * Math.Abs(NetQty);
            }
            else if (NetQty > 0)
            {
                UnrealPnl = (_mid - AvgFillPx) * 100 * NetQty;
            }
        }

        public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
        {
            try
            {
                string symbol = key.Item1;
                if (value == null || symbol != Symbol)
                {
                    return;
                }
                else if (value is OMSSendPosition position)
                {
                    Position.NetQty = position.NetQty;
                    NetQty = position.NetQty;
                    AvgFillPx = position.AveCost;
                    RealPnl = position.RealizedPL;
                    UpdateUnreal();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscibedPositionUpdateValue)}");
            }
        }

        internal void Updated()
        {
            NetDelta = _delta * Position.ActualQty * Multiplier;
            NetGamma = _gamma * Position.ActualQty * Multiplier;
            NetTheta = _theta * Position.ActualQty * Multiplier;
            IvVegaPnl = (_iv - _initialIv) * Multiplier * _vega * Position.ActualQty * Multiplier;
            GammaTheta = NetTheta != 0 ? NetGamma / NetTheta : 0;
        }

        internal void Dispose()
        {
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Delta, this);
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Gamma, this);
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Theta, this);
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.ImpliedVol, this);
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Vega, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Delta, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Gamma, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Theta, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.ImpliedVol, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Vega, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
            OmsCore.QuoteClient.Unsubscribe(_atmSymbol, SubscriptionFieldType.Vega, this);
        }

        internal void ResetPositionPnl()
        {
            AvgFillPx = double.NaN;
        }

        internal void UpdateGreeks(double mid)
        {
            Comms.Models.Data.MarketData.MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(Option.UnderlyingSymbol);
            double totalDays = (Option.Expiration - DateTime.Now).TotalDays;
            if (totalDays < 0.0)
            {
                totalDays = 0;
            }
            else
            {
                totalDays += 1;
            }
            PricingParameters pricingParameters = new()
            {
                Volatility = 0.0,
                PutCall = Option.Type == Data.Securities.OptionType.PUT ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                Strike = Option.Strike,
                DaysToExpiration = totalDays,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = mid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = Option.UnderlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, mid, underlyingDetails.Dividends, DateTime.Now);
            Greeks greeks = new();
            _iv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, _mid, greeks);
            _delta = greeks.Delta;
            _gamma = greeks.Gamma;
            _theta = greeks.Theta;
            _vega = greeks.Vega;
            double weightedVega = _atmVega != 0 ? _vega / _atmVega : 0;
            NetWeightedVega = weightedVega * Position.ActualQty * 100;
            if ((double.IsNaN(_initialIv) || _initialIv == 0) && Position.ActualQty != 0)
            {
                _initialIv = _iv;
            }
        }

        internal Greeks GetGreeks(double mid)
        {
            Comms.Models.Data.MarketData.MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(Option.UnderlyingSymbol);
            double totalDays = (Option.Expiration - DateTime.Now).TotalDays;
            if (totalDays < 0.0)
            {
                totalDays = 0;
            }
            else
            {
                totalDays += 1;
            }
            PricingParameters pricingParameters = new()
            {
                Volatility = 0.0,
                PutCall = Option.Type == Data.Securities.OptionType.PUT ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                Strike = Option.Strike,
                DaysToExpiration = totalDays,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = mid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = Option.UnderlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, mid, underlyingDetails.Dividends, DateTime.Now);
            Greeks greeks = new();
            OptionModel.Binomial.ImpliedVolatility(pricingParameters, _mid, greeks);
            return greeks;
        }
    }
}
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PositionAnalyzerViewModel : CustomizableTableViewModelBase, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private DispatcherTimer _updateTimer = new();

        private RiskProfileModel _liveProfile;

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string InputString { get; set; }

        [Bindable]
        public partial string Underlying { get; set; }

        [Bindable]
        public partial double Mid { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double BasePrice { get; set; }

        [Bindable]
        public partial int DaysToExpiration { get; set; }

        [Bindable]
        public partial int DaysToExpirationOffSet { get; set; }

        [Bindable(Default = 1)]
        public partial int Quantity { get; set; }

        [Bindable]
        public partial Side Side { get; set; }

        [Bindable(Default = .01)]
        public partial double StepPercent { get; set; }

        [Bindable(Default = 0)]
        public partial double IvOffset { get; set; }

        [Bindable(Default = 21)]
        public partial int Count { get; set; }

        public OmsCore OmsCore { get; }
        public ObservableCollection<SymbolModel> Positions { get; set; }
        public FastObservableCollection<RiskProfileModel> PositionsRisk { get; set; }
        public IEnumerable<Side> Sides { get; } = ((Side[])Enum.GetValues(typeof(Side))).ToList();

        public bool IsDisposed { get; set; }

        public PositionAnalyzerViewModel()
        {
            _updateTimer.Interval = TimeSpan.FromMilliseconds(750);
            _updateTimer.Tick += OnUpdateTimer_Tick;


            OmsCore = ServiceLocator.GetService<OmsCore>();
            Positions = new ObservableCollection<SymbolModel>();
            PositionsRisk = new FastObservableCollection<RiskProfileModel>();
            ModuleTitle = "Position Analyzer";

            UpdatePositionsList();
        }

        [Command]
        public void UpdatePositionsList()
        {
            try
            {
                List<RiskProfileModel> tempList = new();
                RiskProfileModel riskPosition = new()
                {
                    IsCurrent = true,
                    Percentage = 0
                };
                _liveProfile = riskPosition;
                tempList.Add(riskPosition);

                double prev = 0.0;
                for (int i = 1; i < Count; i++)
                {
                    double nextStep = i % 2 == 0 ? prev : prev + StepPercent;
                    double percentage = i % 2 == 0 ? -nextStep : nextStep;
                    riskPosition = new RiskProfileModel
                    {
                        Percentage = percentage
                    };
                    prev = nextStep;
                    tempList.Add(riskPosition);
                }

                PositionsRisk?.Clear();
                PositionsRisk?.AddRange(tempList.OrderBy(x => x.Percentage).ToList());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdatePositionsList));
            }
        }

        [Command]
        public async Task AddCommand()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(InputString))
                {
                    return;
                }

                SymbolCodec symbolCodec = new(InputString);
                symbolCodec.Normalize();
                InputString = symbolCodec.ToTOS();
                if (symbolCodec != null && symbolCodec.LegCount > 0)
                {
                    string underlying = symbolCodec.UnderlyingSymbol();
                    List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbols(underlying);
                    Unload();
                    for (int i = 0; i < symbolCodec.LegCount; i++)
                    {
                        Instrument instrument = symbolCodec.GetLeg(i);
                        Data.Securities.Option option = symbols?.FirstOrDefault(x => x.OptionSymbol == instrument.symbol);
                        SymbolModel model = new()
                        {
                            Underlying = underlying,
                            Symbol = instrument.symbol,
                            Quantity = instrument.ratio,
                            Side = instrument.buySell ? Side.Buy : Side.Sell,
                            Expiration = instrument.expiration,
                            Strike = instrument.strike,
                            PutCall = instrument.callPut ? PutCall.Put : PutCall.Call,
                            Multiplier = option != null ? option.Multiplier : 100,
                        };
                        Positions.Add(model);
                    }
                    Underlying = underlying;
                    Load();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddCommand));
            }
        }

        [Command]
        public void RemoveCommand(SymbolModel symbolModel)
        {
            Positions.Remove(symbolModel);
        }

        private void OnUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateRisk(Mid);
        }

        private void Load()
        {
            try
            {
                foreach (SymbolModel model in Positions)
                {
                    model.Subscribe();
                }

                SymbolModel symbolModel = Positions.OrderBy(x => x.Expiration).FirstOrDefault();
                DaysToExpiration = (int)Math.Max(0, Math.Ceiling(symbolModel != null ? (symbolModel.Expiration - DateTime.Now).TotalDays : 0));
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                OmsCore.GreekClient.Subscribe(InputString, SubscriptionFieldType.ImpliedVol, this);
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }

        private void Unload()
        {
            try
            {
                _updateTimer.Stop();
                foreach (SymbolModel model in Positions)
                {
                    model.Unsubscribe();
                }
                Positions.Clear();
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                OmsCore.GreekClient.UnsubscribeAll(SubscriptionFieldType.ImpliedVol, this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Unload));
            }
        }

        private void UpdateRisk(double mid)
        {
            try
            {
                int count = PositionsRisk.Count;

                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(Underlying);
                if (underlyingDetails != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        RiskProfileModel riskProfileModel = PositionsRisk[i];
                        double percentage = Math.Round(riskProfileModel.Percentage, 2);
                        double newMid = mid + (mid * percentage);
                        riskProfileModel.UnderlyingPrice = newMid;

                        double netDelta = 0.0;
                        double netGamma = 0.0;
                        double netTheta = 0.0;
                        double netMid = 0.0;
                        double multiplier = 0.0;

                        for (int j = 0; j < Positions.Count; j++)
                        {
                            SymbolModel position = Positions[j];
                            Greeks greek = position.UpdateGreeks(underlyingDetails, newMid, DaysToExpirationOffSet, IvOffset);
                            int quantity = position.Side == Side.Buy ? position.Quantity : -position.Quantity;
                            multiplier = position.Multiplier;
                            netDelta += greek.Delta * quantity * position.Multiplier;
                            netGamma += greek.Gamma * quantity * position.Multiplier;
                            netTheta += greek.Theta * quantity * position.Multiplier;
                            netMid += position.NewMid * quantity;
                        }

                        riskProfileModel.NetDelta = netDelta;
                        riskProfileModel.NetGamma = netGamma;
                        riskProfileModel.NetTheta = netTheta;

                        riskProfileModel.Pnl = Side == Side.Buy ? (Math.Abs(netMid) - Math.Abs(BasePrice)) * Quantity * multiplier : (Math.Abs(BasePrice) - Math.Abs(netMid)) * Quantity * multiplier;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateRisk));
            }
        }

        internal void Dispose()
        {
            Unload();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            SubscriptionFieldType subscriptionFieldType = key.Type;

            if (symbol == Underlying &&
                subscriptionFieldType == SubscriptionFieldType.MidPoint &&
                value is double mid)
            {
                Mid = mid;
            }
        }
    }
}

using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models.Volatility;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Helper;
using SymbolLib;
using System.Threading;
using System.ComponentModel;
using Option = ZeroPlus.Oms.Data.Securities.Option;

namespace ZeroPlus.Oms.Ui.ViewModels.Volatility
{
    public class OptionsVisualizerViewModel : CustomizableTableViewModelBase, IOmsDataSubscriber, IDisposable
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsCore _omsCore;
        private List<Option> _fullOptionChain = [];
        private readonly SpreadStrategy _strategy;
        private string _underlyingSymbol;
        private bool _isDisposed;
        private string _sourceSymbol;
        private CancellationTokenSource _recalculationCts;

        public event Action<string, string> SpreadUpdated;

        public OptionsVisualizerViewModel(OmsCore omsCore)
        {
            _omsCore = omsCore ?? throw new ArgumentNullException(nameof(omsCore));
            _strategy = new SpreadStrategy();
            Legs = new ObservableCollection<LegViewModel>();
            Legs.CollectionChanged += Legs_CollectionChanged;

            InitializeMarketData();

            // Commands
            ResetDefaultsCommand = new DelegateCommand(ResetToDefaults);
            AddLegCommand = new DelegateCommand(AddLeg);
            RemoveLegCommand = new DelegateCommand<LegViewModel>(RemoveLeg);

            ReferencePrice = 100; // Default until market data
            RiskFreeRate = 0.02;  // Default
            ImpliedVolatility = 0.20; // Default
            EvaluationDate = DateTime.Today;

            // Default Strategy
            // No legs by default
            Recalculate();
        }

        public void LoadStrategy(SpreadQuotesAndGreeksModel model)
        {
            if (model == null) return;

            _sourceSymbol = model.Symbol;
            UnderlyingSymbol = model.Underlying;
            InitializeIvFromRaptor(model.Symbol);
            Legs.Clear();

            foreach (var legModel in model.SpreadLegs)
            {
                var security = new SymbolLib.Instrument(legModel.Symbol);
                if (security.valid)
                {
                    var side = legModel.Side == Side.Buy ? PositionSide.Long : PositionSide.Short;
                    int qty = Math.Abs(legModel.Ratio);
                    OptionType type = security.callPut ? OptionType.Put : OptionType.Call;
                    double strike = security.strike;
                    DateTime expiry = security.expiration;

                    Legs.Add(CreateLeg(side, type, strike, expiry, qty));
                }
            }

            Recalculate();
        }

        public void LoadStrategy(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return;

            _sourceSymbol = symbol;

            var codec = new SymbolCodec(symbol);
            if (codec.LegCount > 0)
            {
                UnderlyingSymbol = codec.UnderlyingSymbol();
                InitializeIvFromRaptor(symbol);
                Legs.Clear();

                for (int i = 0; i < codec.LegCount; i++)
                {
                    var legInfo = codec.GetLeg(i);
                    try
                    {
                        var optionDetails = OptionsHelper.GetOptionFromSymbol(legInfo.symbol);
                        if (optionDetails != null)
                        {
                            var side = legInfo.buySell ? PositionSide.Long : PositionSide.Short;
                            int qty = (int)Math.Abs(legInfo.ratio);
                            var uiType = optionDetails.Type.ToString().ToUpper().Contains("CALL") ? OptionType.Call : OptionType.Put;
                            Legs.Add(CreateLeg(side, uiType, optionDetails.Strike, optionDetails.Expiration, qty));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Invalid leg symbol '{legInfo.symbol}': {ex.Message}", ex);
                    }
                }
                Recalculate();
                return;
            }

            var security = new SymbolLib.Instrument(symbol);
            if (!security.valid)
            {
                throw new ArgumentException($"Could not parse symbol '{symbol}' as a valid strategy.");
            }

            UnderlyingSymbol = security.underlyingSymbol;
            InitializeIvFromRaptor(symbol);
            Legs.Clear();

            var simpleSide = PositionSide.Long;
            int simpleQty = 1;
            OptionType type = security.callPut ? OptionType.Put : OptionType.Call;
            Legs.Add(CreateLeg(simpleSide, type, security.strike, security.expiration, simpleQty));

            Recalculate();
        }

        public void LoadStrategy(QuotesAndGreeksModel model)
        {
            if (model == null) return;

            _sourceSymbol = model.Symbol;
            UnderlyingSymbol = model.Underlying;

            if (model.VolaV0Vol > 0)
            {
                ImpliedVolatility = model.VolaV0Vol;
            }
            else
            {
                InitializeIvFromRaptor(model.Symbol);
            }

            Legs.Clear();

            var security = new SymbolLib.Instrument(model.Symbol);
            if (security.valid)
            {
                var side = PositionSide.Long;
                int qty = 1;
                OptionType type = security.callPut ? OptionType.Put : OptionType.Call;
                double strike = security.strike;
                DateTime expiry = security.expiration;

                Legs.Add(CreateLeg(side, type, strike, expiry, qty));
            }

            Recalculate();
        }

        private void InitializeIvFromRaptor(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return;
            _omsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DeltaAdjTheo, this);
        }

        public DelegateCommand ResetDefaultsCommand { get; }
        public DelegateCommand AddLegCommand { get; }
        public DelegateCommand<LegViewModel> RemoveLegCommand { get; }

        public FastObservableCollection<double> StrikesList { get; private set; }
        public FastObservableCollection<DateTime> ExpiriesList { get; private set; }

        public bool IsDisposed
        {
            get => _isDisposed;
            set => _isDisposed = value;
        }

        public string UnderlyingSymbol
        {
            get => _underlyingSymbol;
            set
            {
                var upper = value?.ToUpper();
                if (_underlyingSymbol != upper)
                {
                    _underlyingSymbol = upper;
                    RaisePropertyChanged();
                    SubscribeToMarketData();
                    _ = UpdateMarketRatesAsync(_underlyingSymbol);
                    _ = UpdateOptionChainAsync(_underlyingSymbol);
                }
            }
        }

        private void SubscribeToMarketData()
        {
            if (string.IsNullOrWhiteSpace(UnderlyingSymbol)) return;
            _omsCore.QuoteClient.Subscribe(UnderlyingSymbol, SubscriptionFieldType.LastPrice, this);
            _omsCore.QuoteClient.Subscribe(UnderlyingSymbol, SubscriptionFieldType.Bid, this);
            _omsCore.QuoteClient.Subscribe(UnderlyingSymbol, SubscriptionFieldType.Ask, this);
        }

        private async System.Threading.Tasks.Task UpdateMarketRatesAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return;

            try
            {
                var details = await _omsCore.QuoteClient.GetUnderlyingDetailsAsync(symbol);
                if (details != null)
                {
                    RiskFreeRate = details.RiskFreeRate;
                    BorrowRate = details.StockRate;
                    Recalculate();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to update market rates");
            }
        }

        private async Task UpdateOptionChainAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return;

            try
            {
                var options = await _omsCore.QuoteClient.GetSymbolsAsync(symbol);
                _fullOptionChain = options ?? [];

                // Process data on background thread to avoid UI freeze
                var (distinctStrikes, distinctExpiries) = await Task.Run(() =>
                {
                    var strikes = _fullOptionChain.Select(o => o.Strike).Distinct().OrderBy(s => s).ToList();
                    var expiries = _fullOptionChain.Select(o => o.Expiration.Date).Distinct().OrderBy(e => e).ToList();
                    return (strikes, expiries);
                });

                // Updates back on the UI thread (captured context)
                StrikesList.Clear();
                StrikesList.AddRange(distinctStrikes);

                ExpiriesList.Clear();
                ExpiriesList.AddRange(distinctExpiries);

                UpdateEvaluationDateLimit();

                // Validate existing legs against the new chain
                foreach (var leg in Legs)
                {
                    ValidateLeg(leg);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to update option chain drop-downs");
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
        {
            if (key.Type == SubscriptionFieldType.DeltaAdjTheo && value is DeltaAdjTheo dat)
            {
                if (dat.ModelId == 0 && dat.SecondaryVol > 0)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ImpliedVolatility = dat.SecondaryVol;
                    });
                    _omsCore.UpdateManager.Unsubscribe(key.Symbol, SubscriptionFieldType.DeltaAdjTheo, this);
                }
                return;
            }

            if (key.Symbol != UnderlyingSymbol) return;

            double newPrice = ReferencePrice;
            if (key.Type == SubscriptionFieldType.LastPrice && value is double last && last > 0)
            {
                newPrice = last;
            }

            if (Math.Abs(newPrice - ReferencePrice) > 0.01)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ReferencePrice = newPrice;
                });
            }
        }

        private void InitializeMarketData()
        {
            StrikesList = new FastObservableCollection<double>();
            ExpiriesList = new FastObservableCollection<DateTime>();
            MaxEvaluationDate = DateTime.Today.AddYears(2);
        }

        private void ResetToDefaults()
        {
            try
            {
                ReferencePrice = 100;
                ImpliedVolatility = 0.20;
                RiskFreeRate = 0.02;
                EvaluationDate = DateTime.Today;
                Legs.Clear();
                Recalculate();
            }
            catch (Exception ex)
            {
                HandleException(new ArgumentException($"Failed to reset defaults: {ex.Message}", ex), nameof(ResetToDefaults));
            }
        }

        private void ValidateLeg(LegViewModel leg)
        {
            if (_fullOptionChain == null || !_fullOptionChain.Any())
            {
                leg.IsValid = true;
                return;
            }

            bool exists = _fullOptionChain.Any(o =>
                o.Expiration.Date == leg.ExpiryDate.Date &&
                Math.Abs(o.Strike - leg.Strike) < 0.001 &&
                o.Type.ToString().Equals(leg.Type.ToString(), StringComparison.OrdinalIgnoreCase));

            leg.IsValid = exists;
        }

        private void HandleException(Exception ex, string caller)
        {
            _log.Error(ex, caller);
            if (ex is ArgumentException)
            {
                MessageBoxService?.ShowMessage(ex.Message, "Error", MessageButton.OK, MessageIcon.Error);
            }
        }

        private LegViewModel CreateLeg(PositionSide side, OptionType type, double strike, DateTime expiry, int qty = 1)
        {
            var leg = new LegViewModel
            {
                Side = side,
                Type = type,
                Strike = strike,
                ExpiryDate = expiry,
                Quantity = qty
            };

            double t = leg.TimeToExpiryYears(DateTime.Today);
            leg.EntryPrice = BlackScholesCalculator.Price(type, ReferencePrice, strike, RiskFreeRate, ImpliedVolatility, t, BorrowRate);

            ValidateLeg(leg);

            return leg;
        }

        private void OnLegPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is LegViewModel leg)
            {
                if (e.PropertyName is nameof(LegViewModel.ExpiryDate) or nameof(LegViewModel.Strike) or nameof(LegViewModel.Type))
                {
                    ValidateLeg(leg);
                }

                if (e.PropertyName == nameof(LegViewModel.ExpiryDate))
                {
                    UpdateEvaluationDateLimit();
                }
            }
            Recalculate();
        }

        private void AddLeg()
        {
            try
            {
                Legs.Add(CreateLeg(PositionSide.Long, OptionType.Call, ReferencePrice, DateTime.Today.AddMonths(1)));
                Recalculate();
            }
            catch (Exception ex)
            {
                HandleException(new ArgumentException($"Failed to add leg: {ex.Message}", ex), nameof(AddLeg));
            }
        }

        private void RemoveLeg(LegViewModel leg)
        {
            try
            {
                if (leg != null && Legs.Contains(leg))
                {
                    Legs.Remove(leg);
                    Recalculate();
                }
            }
            catch (Exception ex)
            {
                HandleException(new ArgumentException($"Failed to remove leg: {ex.Message}", ex), nameof(RemoveLeg));
            }
        }

        private void Legs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (LegViewModel oldLeg in e.OldItems)
                {
                    oldLeg.PropertyChanged -= OnLegPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (LegViewModel newLeg in e.NewItems)
                {
                    newLeg.PropertyChanged += OnLegPropertyChanged;
                }
            }

            UpdateEvaluationDateLimit();
            Recalculate();
        }

        private void UpdateEvaluationDateLimit()
        {
            DateTime maxDate;

            // 1. If we have legs, the strategy is limited by the nearest expiration
            if (Legs.Any())
            {
                maxDate = Legs.Min(l => l.ExpiryDate);
            }
            // 2. If no legs, but we have a chain, allow exploring the whole chain
            else if (ExpiriesList != null && ExpiriesList.Any())
            {
                maxDate = ExpiriesList.Max();
            }
            // 3. Fallback
            else
            {
                maxDate = DateTime.Today.AddYears(2);
            }

            // Ensure we don't set a max date in the past relative to today (prevents negative slider range)
            if (maxDate < DateTime.Today) maxDate = DateTime.Today;

            MaxEvaluationDate = maxDate;

            // Clamp current evaluation date
            if (EvaluationDate > MaxEvaluationDate)
            {
                EvaluationDate = MaxEvaluationDate;
            }
            else if (EvaluationDate < DateTime.Today)
            {
                EvaluationDate = DateTime.Today;
            }
        }

        #region Global Parameters

        public double ReferencePrice
        {
            get => _strategy.ReferencePrice;
            set { if (_strategy.ReferencePrice != value) { _strategy.ReferencePrice = value; RaisePropertyChanged(); Recalculate(); } }
        }

        public double ImpliedVolatility
        {
            get => _strategy.ImpliedVol;
            set { if (_strategy.ImpliedVol != value) { _strategy.ImpliedVol = value; RaisePropertyChanged(); Recalculate(); } }
        }

        public double RiskFreeRate
        {
            get => _strategy.RiskFreeRate;
            set { if (_strategy.RiskFreeRate != value) { _strategy.RiskFreeRate = value; RaisePropertyChanged(); Recalculate(); } }
        }

        public double BorrowRate
        {
            get => _strategy.BorrowRate;
            set { if (_strategy.BorrowRate != value) { _strategy.BorrowRate = value; RaisePropertyChanged(); Recalculate(); } }
        }

        public DateTime EvaluationDate
        {
            get => GetProperty(() => EvaluationDate);
            set { SetProperty(() => EvaluationDate, value, Recalculate); RaisePropertyChanged(nameof(EvaluationDateOADate)); }
        }

        public double EvaluationDateOADate
        {
            get => EvaluationDate.ToOADate();
            set => EvaluationDate = DateTime.FromOADate(value);
        }

        public double MinEvaluationDateOADate => DateTime.Today.ToOADate();
        public double MaxEvaluationDateOADate => MaxEvaluationDate.ToOADate();

        public DateTime MaxEvaluationDate
        {
            get => GetProperty(() => MaxEvaluationDate);
            set { SetProperty(() => MaxEvaluationDate, value); RaisePropertyChanged(nameof(MaxEvaluationDateOADate)); }
        }

        public ObservableCollection<LegViewModel> Legs { get; }

        #endregion

        private ObservableCollection<ChartDataPoint> _chartPoints = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> ChartPoints
        {
            get => _chartPoints;
            set
            {
                if (_chartPoints != value)
                {
                    _chartPoints = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double _maxProfitValue;
        public string MaxProfit => _maxProfitValue > 1000000 ? "Unlimited" : _maxProfitValue.ToString("C2");

        private double _maxLossValue;
        public string MaxLoss => _maxLossValue < -1000000 ? "Unlimited" : _maxLossValue.ToString("C2");

        public double MinDelta => ChartPoints.Any() ? ChartPoints.Min(p => p.Delta) : 0;
        public double MaxDelta => ChartPoints.Any() ? ChartPoints.Max(p => p.Delta) : 0;
        public double MinGamma => ChartPoints.Any() ? ChartPoints.Min(p => p.Gamma) : 0;
        public double MaxGamma => ChartPoints.Any() ? ChartPoints.Max(p => p.Gamma) : 0;
        public double MinTheta => ChartPoints.Any() ? ChartPoints.Min(p => p.Theta) : 0;
        public double MaxTheta => ChartPoints.Any() ? ChartPoints.Max(p => p.Theta) : 0;
        public double MinVega => ChartPoints.Any() ? ChartPoints.Min(p => p.Vega) : 0;
        public double MaxVega => ChartPoints.Any() ? ChartPoints.Max(p => p.Vega) : 0;


        private void Recalculate()
        {
            _recalculationCts?.Cancel();
            _recalculationCts = new CancellationTokenSource();
            var token = _recalculationCts.Token;

            CheckForSpreadUpdate();
            _ = RecalculateAsync(token);
        }

        private void CheckForSpreadUpdate()
        {
            if (string.IsNullOrEmpty(_sourceSymbol)) return;

            string currentSymbol = BuildEncodedSymbol();

            if (!string.Equals(currentSymbol, _sourceSymbol, StringComparison.OrdinalIgnoreCase))
            {
                SpreadUpdated?.Invoke(_sourceSymbol, currentSymbol);
                _sourceSymbol = currentSymbol;
            }
        }

        private string BuildEncodedSymbol()
        {
            if (Legs == null || !Legs.Any()) return UnderlyingSymbol ?? string.Empty;

            if (Legs.Count == 1)
            {
                var leg = Legs[0];
                return OptionsHelper.GetSymbolFromComponents(UnderlyingSymbol, leg.ExpiryDate, leg.Type == OptionType.Call ? "C" : "P", leg.Strike);
            }

            var parts = new List<string>();
            foreach (var leg in Legs)
            {
                string sign = leg.Side == PositionSide.Long ? "+" : "-";
                string qty = leg.Quantity.ToString();
                string optionSymbol = OptionsHelper.GetSymbolFromComponents(UnderlyingSymbol, leg.ExpiryDate, leg.Type == OptionType.Call ? "C" : "P", leg.Strike);
                parts.Add($"{sign}{qty} {optionSymbol}");
            }

            return string.Join(" ", parts);
        }

        private async Task RecalculateAsync(CancellationToken token)
        {
            // Removed Task.Delay to prevent starvation during high-frequency market data updates
            if (token.IsCancellationRequested) return;

            if (ChartPoints == null) return;

            var legsSnapshot = Legs.Select(l => new
            {
                l.Side,
                l.Type,
                l.Strike,
                l.Quantity,
                l.EntryPrice,
                ExpiryDate = l.ExpiryDate
            }).ToList();

            double refPrice = ReferencePrice;
            double iv = Math.Max(0.0001, ImpliedVolatility);
            double r = RiskFreeRate;
            double qRate = BorrowRate;
            DateTime evalDate = EvaluationDate;

            var result = await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return null;

                    var points = new List<ChartDataPoint>();

                    double minStrike = refPrice > 0 ? refPrice : 100;
                    double maxStrike = minStrike;

                    if (legsSnapshot.Any())
                    {
                        minStrike = Math.Min(minStrike, legsSnapshot.Min(l => l.Strike));
                        maxStrike = Math.Max(maxStrike, legsSnapshot.Max(l => l.Strike));
                    }

                    double center = refPrice > 0 ? refPrice : 100;
                    double rangeSpan = maxStrike - minStrike;
                    double minSpan = center * 0.2;
                    if (rangeSpan < minSpan) rangeSpan = minSpan;
                    double padding = rangeSpan * 0.2;

                    if (!legsSnapshot.Any())
                    {
                        padding = center * 0.2;
                        minStrike = center;
                        maxStrike = center;
                    }

                    double minPrice = minStrike - padding;
                    double maxPrice = maxStrike + padding;
                    if (minPrice <= 0) minPrice = 0.01;

                    int stepCount = 100;
                    double step = (maxPrice - minPrice) / stepCount;

                    for (int i = 0; i <= stepCount; i++)
                    {
                        if (token.IsCancellationRequested) return null;
                        double price = minPrice + (i * step);
                        double currentPnL = 0;
                        double expiryPnL = 0;
                        double totalDelta = 0;
                        double totalGamma = 0;
                        double totalTheta = 0;
                        double totalVega = 0;

                        foreach (var leg in legsSnapshot)
                        {
                            double t = 0;
                            if (leg.ExpiryDate > evalDate)
                            {
                                t = (leg.ExpiryDate - evalDate).TotalDays / 365.0;
                            }
                            if (t < 0) t = 0;

                            double valueAtEval = BlackScholesCalculator.Price(leg.Type, price, leg.Strike, r, iv, t, qRate);
                            double valueAtExpiry = BlackScholesCalculator.Price(leg.Type, price, leg.Strike, r, iv, 0, qRate);
                            double delta = BlackScholesCalculator.Delta(leg.Type, price, leg.Strike, r, iv, t, qRate);
                            double gamma = BlackScholesCalculator.Gamma(price, leg.Strike, r, iv, t, qRate);
                            double theta = BlackScholesCalculator.Theta(leg.Type, price, leg.Strike, r, iv, t, qRate);
                            double vega = BlackScholesCalculator.Vega(price, leg.Strike, r, iv, t, qRate);

                            int sideSign = leg.Side == PositionSide.Long ? 1 : -1;
                            currentPnL += (valueAtEval - leg.EntryPrice) * leg.Quantity * sideSign;
                            expiryPnL += (valueAtExpiry - leg.EntryPrice) * leg.Quantity * sideSign;
                            totalDelta += delta * sideSign * leg.Quantity;
                            totalGamma += gamma * sideSign * leg.Quantity;
                            totalTheta += theta * sideSign * leg.Quantity;
                            totalVega += vega * sideSign * leg.Quantity;
                        }

                        points.Add(new ChartDataPoint
                        {
                            Price = price,
                            Payoff = currentPnL,
                            PayoffProfit = Math.Max(0, currentPnL),
                            PayoffLoss = Math.Min(0, currentPnL),
                            ExpiryPayoff = expiryPnL,
                            Delta = totalDelta * 100,
                            Gamma = totalGamma * 100,
                            Theta = totalTheta * 100,
                            Vega = totalVega * 100
                        });
                    }
                    return points;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Calculation failed");
                    return null;
                }
            }, token);

            if (result == null || token.IsCancellationRequested) return;

            // Efficient UI Update: Update existing points to prevent chart flickering
            if (ChartPoints.Count != result.Count)
            {
                ChartPoints = new ObservableCollection<ChartDataPoint>(result);
            }
            else
            {
                for (int i = 0; i < ChartPoints.Count; i++)
                {
                    ChartPoints[i].UpdateFrom(result[i]);
                }
            }

            if (ChartPoints.Any())
            {
                _maxProfitValue = ChartPoints.Max(p => p.Payoff);
                _maxLossValue = ChartPoints.Min(p => p.Payoff);
                RaisePropertyChanged(nameof(MaxProfit));
                RaisePropertyChanged(nameof(MaxLoss));
                RaisePropertyChanged(nameof(MinDelta));
                RaisePropertyChanged(nameof(MaxDelta));
                RaisePropertyChanged(nameof(MinGamma));
                RaisePropertyChanged(nameof(MaxGamma));
                RaisePropertyChanged(nameof(MinTheta));
                RaisePropertyChanged(nameof(MaxTheta));
                RaisePropertyChanged(nameof(MinVega));
                RaisePropertyChanged(nameof(MaxVega));
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (!string.IsNullOrWhiteSpace(UnderlyingSymbol))
                {
                    _omsCore.QuoteClient.Unsubscribe(UnderlyingSymbol, SubscriptionFieldType.LastPrice, this);
                    _omsCore.QuoteClient.Unsubscribe(UnderlyingSymbol, SubscriptionFieldType.Bid, this);
                    _omsCore.QuoteClient.Unsubscribe(UnderlyingSymbol, SubscriptionFieldType.Ask, this);
                }

                _recalculationCts?.Cancel();
                _recalculationCts?.Dispose();

                foreach (var leg in Legs)
                {
                    leg.PropertyChanged -= OnLegPropertyChanged;
                }
            }
        }
    }

    public class ChartDataPoint : BindableBase
    {
        public double Price { get => GetProperty(() => Price); set => SetProperty(() => Price, value); }
        public double Payoff { get => GetProperty(() => Payoff); set => SetProperty(() => Payoff, value); }
        public double PayoffProfit { get => GetProperty(() => PayoffProfit); set => SetProperty(() => PayoffProfit, value); }
        public double PayoffLoss { get => GetProperty(() => PayoffLoss); set => SetProperty(() => PayoffLoss, value); }
        public double ExpiryPayoff { get => GetProperty(() => ExpiryPayoff); set => SetProperty(() => ExpiryPayoff, value); }
        public double Delta { get => GetProperty(() => Delta); set => SetProperty(() => Delta, value); }
        public double Gamma { get => GetProperty(() => Gamma); set => SetProperty(() => Gamma, value); }
        public double Theta { get => GetProperty(() => Theta); set => SetProperty(() => Theta, value); }
        public double Vega { get => GetProperty(() => Vega); set => SetProperty(() => Vega, value); }

        public void UpdateFrom(ChartDataPoint other)
        {
            Price = other.Price;
            Payoff = other.Payoff;
            PayoffProfit = other.PayoffProfit;
            PayoffLoss = other.PayoffLoss;
            ExpiryPayoff = other.ExpiryPayoff;
            Delta = other.Delta;
            Gamma = other.Gamma;
            Theta = other.Theta;
            Vega = other.Vega;
        }
    }
}

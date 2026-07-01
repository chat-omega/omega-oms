using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Generators;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms.Clients
{
    public class QuoteClient : SubscriptionProvider, IOmsDataSubscriber
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly CommsClient _marketDataChannel;
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _underlyingToSymbolsLoadedNotifierMap = new();
        private readonly ConcurrentDictionary<string, MDUnderlying> _underlyingToUnderlyingDetailsMap = new();
        private bool _isConnected;
        private readonly OmsCore _omsCore;
        private readonly object _databentoSubscriptionsLock = new();
        private readonly ConcurrentDictionary<string, int> _databentoSubscriptions = [];
        private readonly ConcurrentDictionary<SubscriptionKey, byte> _databentoTrackedQuoteSubscriptions = [];
        private QuoteSource _activeQuoteSource;

        public DateTime ServerTime { get; set; }
        public double ServerCreepMs { get; private set; } = double.NaN;
        public bool IsDisposed { get; set; }
        public OptionsLookup OptionsLookup { get; } = new();

        public QuoteSource ActiveQuoteSource
        {
            get => _activeQuoteSource;
            set
            {
                if (_activeQuoteSource == value)
                {
                    return;
                }
                _log.Info($"ActiveQuoteSource changing from {_activeQuoteSource} to {value}");
                _activeQuoteSource = value;
                OnPropertyChanged();
                Resubscribe();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (value != _isConnected)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        public QuoteClient(OmsConfig config, OmsCore omsCore)
        {
            _omsCore = omsCore;
            _marketDataChannel = new CommsClient(OmsConfig.MdGuid, config, HandleMessage, omsCore, register: true);
            Config = config;
            _activeQuoteSource = config.QuoteSource;
            ImpliedStore = new ImpliedStore(this);
            _marketDataChannel.ConnectionStatusChangedEvent += OnMarketDataConnectionStatusChangedEvent;
            omsCore.GatewayClient.EntitlementUpdated += OnEntitlementUpdate;
            config.PropertyChanged += OnConfigPropertyChanged;
        }

        private void OnEntitlementUpdate()
        {
            Resubscribe();
        }

        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OmsConfig.QuoteSource))
            {
                ActiveQuoteSource = Config.QuoteSource;
            }
        }

        #region PublicMethods
        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            _log.Info(nameof(StartAsync));
            return await Task.Run(() => _marketDataChannel.Start(Config.QuoteAddress, Config.QuotePort));
        }

        public async Task StopAsync()
        {
            await Task.Run(() => _marketDataChannel.Stop());
        }

        public async Task<MDUnderlying> GetUnderlyingDetailsAsync(string underlyingSymbol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(underlyingSymbol))
                {
                    return null;
                }
                underlyingSymbol = underlyingSymbol.ToUpper();
                if (!_underlyingToUnderlyingDetailsMap.TryGetValue(underlyingSymbol, out MDUnderlying underlyingDetails) || underlyingDetails == null)
                {
                    underlyingDetails = await Task.Run(() => GetUnderlyingDetails(underlyingSymbol));
                }
                return underlyingDetails;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetUnderlyingDetailsAsync)} -> Exception getting underlying data");
                return null;
            }
        }

        public MDUnderlying GetUnderlyingDetails(string underlyingSymbol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(underlyingSymbol))
                {
                    _log.Error($"{nameof(GetUnderlyingDetails)} -> Exception getting underlying data. Symbol can not be empty");
                    return null;
                }
                if (!_underlyingToUnderlyingDetailsMap.TryGetValue(underlyingSymbol, out MDUnderlying underlyingDetails) || underlyingDetails == null)
                {
                    underlyingDetails = _marketDataChannel.GetUnderlyingDetails(underlyingSymbol).FirstOrDefault();
                    if (underlyingDetails != null)
                    {
                        _underlyingToUnderlyingDetailsMap[underlyingSymbol] = underlyingDetails;
                    }
                }
                return underlyingDetails;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetUnderlyingDetails)} -> Exception getting underlying data");
                return null;
            }
        }

        public async Task<List<Models.Data.Securities.Option>> GetOptionsAsync(string symbol)
        {
            var results = new List<Models.Data.Securities.Option>();
            try
            {
                var symbols = await GetSymbolsAsync(symbol);
                if (symbols != null)
                {
                    foreach (var option in symbols)
                    {
                        if (_omsCore.SecurityBook.GetSecurity(option.OptionSymbol) is
                            Models.Data.Securities.Option converted)
                        {
                            results.Add(converted);
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetOptionsAsync)} -> Exception getting options");
                return results;
            }
        }

        public async Task<List<Option>> GetSymbolsAsync(string symbol)
        {
            try
            {
                return await Task.Run(() => GetSymbols(symbol));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetSymbolsAsync)} -> Exception getting symbols");
                return new List<Option>();
            }
        }

        public async Task<List<Option>> GetSymbolsWithAttemptAsync(string symbol, int attempt)
        {
            try
            {
                return await Task.Run(async () =>
                {
                    List<Option> result = new();

                    for (int i = 0; i < attempt; i++)
                    {
                        result = await GetSymbols(symbol);
                        if (result.Count == 0 && i <= attempt)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        break;
                    }

                    return result;
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetSymbolsWithAttemptAsync)} -> Exception getting symbols");
                return new List<Option>();
            }
        }

        public async Task<List<Option>> GetSymbols(string symbol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return new List<Option>();
                }

                symbol = symbol.ToUpper();
                List<Option> options;
                if (OptionsLookup.Contains(symbol))
                {
                    options = OptionsLookup.GetAllOptions(symbol);
                }
                else
                {
                    var stopwatch = Stopwatch.StartNew();
                    options = await RequestSymbolsFromServerAsync(symbol);
                    stopwatch.Stop();
                    _log.Info($"Request Symbols from server. Symbol: {symbol}, Results: {options.Count}, Time: {stopwatch.ElapsedMilliseconds}");
                }

                return options;
            }
            catch (SlimException ex)
            {
                _log.Warn(ex, $"{nameof(GetSymbols)} -> Error getting symbols");
                return new List<Option>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetSymbols)} -> Exception getting symbols");
                return new List<Option>();
            }
        }

        private async Task<List<Option>> RequestSymbolsFromServerAsync(string symbol)
        {
            List<Option> options = new();
            if (symbol.Contains('\\'))
            {
                var input = symbol.ToUpper();
                var symbolRoute = input.Split("\\");
                if (symbolRoute.Length > 3)
                {
                    var baseSym = symbolRoute[0];
                    var secType = symbolRoute[1];
                    var exchange = symbolRoute[2];
                    var currency = symbolRoute[3];
                    options = await _omsCore.IbGatewayClient.GetSymbolsAsync(baseSym, secType, exchange, currency);

                    OptionsLookup.AddOptions(symbol, options);
                }

                return options;
            }

            var newEvent = new ManualResetEventSlim(false);
            var loadedResetEvent = _underlyingToSymbolsLoadedNotifierMap.GetOrAdd(symbol, newEvent);
            bool isOwner = ReferenceEquals(loadedResetEvent, newEvent);

            if (isOwner)
            {
                try
                {
                    List<MDOptionExt> symbols = await _marketDataChannel.GetSymbolsExtAsync(symbol);
                    options = new List<Option>();
                    if (symbols == null)
                    {
                        _underlyingToSymbolsLoadedNotifierMap.TryRemove(symbol, out _);
                    }
                    else
                    {
                        foreach (MDOptionExt mdsymbol in symbols)
                        {
                            try
                            {
                                Option option = OptionsHelper.GetOptionFromSymbol(mdsymbol.Symbol);
                                option.TickType = mdsymbol.MinTickStyle;
                                option.MinimumTick = mdsymbol.MinimumTick;
                                option.Multiplier = mdsymbol.Multiplier;
                                _log.Info(nameof(RequestSymbolsFromServerAsync) + " -> Symbol: " + mdsymbol.Symbol +
                                          ", Tick: " + mdsymbol.MinTickStyle + ", MinTick: " + mdsymbol.MinimumTick +
                                          ", Multiplier: " + mdsymbol.Multiplier);
                                options.Add(option);
                            }
                            catch (Exception ex)
                            {
                                _log.Error(
                                    $"{nameof(RequestSymbolsFromServerAsync)} -> Exception parsing {mdsymbol.Symbol} {ex}");
                            }
                        }

                        if (OmsCore.Config.RemoveAdjustedOptions)
                        {
                            options = options.Where(x => x.RootSymbol.Length > 0 && !char.IsDigit(x.RootSymbol[^1]))
                                .ToList();
                        }

                        OptionsLookup.AddOptions(symbol, options, forceUpdate: true);
                    }
                }
                catch (SlimException ex)
                {
                    _log.Warn(ex, $"{nameof(RequestSymbolsFromServerAsync)} -> Exception getting symbols");
                    _underlyingToSymbolsLoadedNotifierMap.TryRemove(symbol, out _);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"{nameof(RequestSymbolsFromServerAsync)} -> Exception getting symbols");
                    _underlyingToSymbolsLoadedNotifierMap.TryRemove(symbol, out _);
                }
                finally
                {
                    if (options.Count == 0)
                    {
                        _underlyingToSymbolsLoadedNotifierMap.TryRemove(symbol, out _);
                    }

                    loadedResetEvent.Set();
                }
            }
            else
            {
                newEvent.Dispose();

                if (!loadedResetEvent.IsSet)
                {
                    loadedResetEvent.Wait(5000);
                }

                if (loadedResetEvent.IsSet)
                {
                    options = OptionsLookup.GetAllOptions(symbol);
                }
            }

            return options;
        }

        #endregion

        private void OnMarketDataConnectionStatusChangedEvent(bool connected)
        {
            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                _underlyingToSymbolsLoadedNotifierMap.Clear();
                _underlyingToUnderlyingDetailsMap.Clear();
                _marketDataChannel.SendMdsClientRegistrationMsg();
                _marketDataChannel.SendSetMdClientNameMsg();
                _marketDataChannel.SubscribeServerCreep();
                _marketDataChannel.SubscribeServerTimeUpdate();
                OptionsLookup.Clear();
                Resubscribe();
                _marketDataChannel.ThrottleMarketData(Config.PerformanceModeEnabled);
            }
        }

        private void HandleMessage(Message message)
        {
            switch (message.Template.TemplateType)
            {
                case TemplateType.MDSendSymbols:
                    MDSendSymbols symbolsMessage = MessageFactory.DecodeMDSendSymbolsMessage(message);
                    HandleSymbols(symbolsMessage);
                    break;
                case TemplateType.MDSendMarketDataDouble:
                    MDSendMarketDataDouble marketDataDouble = MessageFactory.DecodeMDSendMarketDataDoubleMessage(message);
                    HandleMarketDataDoubleMessage(marketDataDouble);
                    break;
                case TemplateType.MDSendGreekData:
                    MDSendGreekData greekMessage = MessageFactory.DecodeMDSendGreekDataMessage(message);
                    HandleGreekMessage(greekMessage);
                    break;
                case TemplateType.MDSendAlerts:
                    MDSendAlerts mdSendAlerts = MessageFactory.DecodeMDSendAlertsMessage(message);
                    HandleAlertMessage(mdSendAlerts);
                    break;
                case TemplateType.MDSendDmitryTrade:
                    MDSendDmitryTrade tronTradeMessage = MessageFactory.DecodeMDSendDmitryTradeMessage(message);
                    HandleTronTradeMessage(tronTradeMessage);
                    break;
            }
        }

        private void HandleSymbols(MDSendSymbols symbolsMessage)
        {
            try
            {
                List<Option> options = new();
                foreach (MDOption option in symbolsMessage.Symbols)
                {
                    options.Add(OptionsHelper.GetOptionFromSymbol(option.Symbol));
                }
                OptionsLookup.AddOptions(symbolsMessage.Symbol, options);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleSymbols));
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
        {
            if (value is DoubleUpdateModel doubleUpdate)
            {
                var symbol = key.Symbol;
                double multiplier = 1;
                HandleDoubleUpdate(symbol, doubleUpdate, multiplier);

                if (Config.ReverseSymbolsLookup.TryGetValue(symbol, out var tuples))
                {
                    foreach (var tuple in tuples)
                    {
                        symbol = tuple.Item1;
                        multiplier = tuple.Item3;
                        HandleDoubleUpdate(symbol, doubleUpdate, multiplier);
                    }
                }
            }
        }

        private void HandleDoubleUpdate(string symbol, DoubleUpdateModel doubleUpdate, double multiplier)
        {
            Update(symbol, SubscriptionFieldType.Bid, doubleUpdate.Bid * multiplier);
            Update(symbol, SubscriptionFieldType.Ask, doubleUpdate.Ask * multiplier);
            Update(symbol, SubscriptionFieldType.MidPoint, doubleUpdate.Mid * multiplier);
            Update(symbol, SubscriptionFieldType.BidSize, doubleUpdate.BidSize);
            Update(symbol, SubscriptionFieldType.AskSize, doubleUpdate.AskSize);
            Update(symbol, SubscriptionFieldType.LastPrice, doubleUpdate.LastPrice * multiplier);
        }

        private void HandleMarketDataDoubleMessage(MDSendMarketDataDouble marketDataDouble)
        {
            try
            {
                string symbol = marketDataDouble.Symbol;
                SubscriptionFieldType type = (SubscriptionFieldType)marketDataDouble.RequestType;

                var update = GetUpdate(marketDataDouble, multiplier: 1);
                Update(marketDataDouble.Symbol, type, update);

                if (Config.ReverseSymbolsLookup.TryGetValue(symbol, out var tuples))
                {
                    foreach (var tuple in tuples)
                    {
                        symbol = tuple.Item1;
                        if (type == SubscriptionFieldType.LastPrice && symbol == "$SPX")
                        {
                            continue;
                        }

                        update = GetUpdate(marketDataDouble, multiplier: tuple.Item3);
                        Update(symbol, type, update);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleMarketDataDoubleMessage));
            }
        }

        private static object GetUpdate(MDSendMarketDataDouble marketDataDouble, double multiplier)
        {
            object update;
            if (marketDataDouble.ErrorCode > 0)
            {
                update = marketDataDouble.ErrorMessage;
            }
            else if (!double.IsNaN(marketDataDouble.Element) && !double.IsInfinity(marketDataDouble.Element))
            {
                update = marketDataDouble.Element * multiplier;
            }
            else
            {
                update = null;
            }

            return update;
        }

        private void HandleTronTradeMessage(MDSendDmitryTrade tronTradeMessage)
        {
            try
            {
                if (tronTradeMessage != null)
                {
                    string symbol = tronTradeMessage.Symbol;
                    SubscriptionFieldType type = SubscriptionFieldType.TronTrade;
                    Update(symbol, type, tronTradeMessage);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleTronTradeMessage));
            }
        }

        private void HandleGreekMessage(MDSendGreekData greekMessage)
        {
            try
            {
                SubscriptionKey subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Greeks);
                if (TryGetSubscribers(subscriptionKey, out var subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    GreekUpdate greekUpdate = new()
                    {
                        Delta = greekMessage.Delta,
                        Gamma = greekMessage.Gamma,
                        Vega = greekMessage.Vega,
                        Theta = greekMessage.Theta,
                        Rho = greekMessage.Rho,
                        Implied = greekMessage.IV,
                        Theo = greekMessage.TV,
                    };

                    subscribers.UpdateValues(greekUpdate);
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Delta);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.Delta) && !double.IsInfinity(greekMessage.Delta))
                    {
                        subscribers.UpdateValues(greekMessage.Delta);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Gamma);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.Gamma) && !double.IsInfinity(greekMessage.Gamma))
                    {
                        subscribers.UpdateValues(greekMessage.Gamma);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Vega);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.Vega) && !double.IsInfinity(greekMessage.Vega))
                    {
                        subscribers.UpdateValues(greekMessage.Vega);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Theta);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.Theta) && !double.IsInfinity(greekMessage.Theta))
                    {
                        subscribers.UpdateValues(greekMessage.Theta);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Rho);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.Rho) && !double.IsInfinity(greekMessage.Rho))
                    {
                        subscribers.UpdateValues(greekMessage.Rho);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.ImpliedVol);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.IV) && !double.IsInfinity(greekMessage.IV))
                    {
                        subscribers.UpdateValues(greekMessage.IV);
                    }
                }

                subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.TheorethicalValue);
                if (TryGetSubscribers(subscriptionKey, out subscribers))
                {
                    if (greekMessage.ErrorCode > 0)
                    {
                        subscribers.UpdateValues(greekMessage.ErrorMessage);
                        return;
                    }

                    if (!double.IsNaN(greekMessage.TV) && !double.IsInfinity(greekMessage.TV))
                    {
                        subscribers.UpdateValues(greekMessage.TV);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleGreekMessage));
            }
        }

        private void HandleAlertMessage(MDSendAlerts alertMessage)
        {
            try
            {
                switch (alertMessage.Type)
                {
                    case AlertType.MDTronCreep:
                        ServerCreepMs = alertMessage.ClockCreep.TotalMilliseconds;
                        break;
                    case AlertType.MDTronClock:
                        ServerTime = alertMessage.LastMDTronClockSeen;
                        SubscriptionKey subscriptionKey = new(string.Empty, SubscriptionFieldType.ServerClockUpdate);
                        if (TryGetSubscribers(subscriptionKey, out var subscribers))
                        {
                            subscribers.UpdateValues(alertMessage.LastMDTronClockSeen);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleAlertMessage));
            }
        }

        public async Task<Option> GetNextExpirationOption(string symbol, PermutationDirection direction)
        {
            Option option = OptionsHelper.GetOptionFromSymbol(symbol);

            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetNextExpiration(option, direction);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public async Task<Option> GetNextStrikeOption(string symbol, PermutationDirection direction)
        {
            Option option = OptionsHelper.GetOptionFromSymbol(symbol);

            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetNextStrike(option, direction);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public async Task<Option> GetNextExpirationOption(Option option, PermutationDirection direction)
        {
            if (option == null)
            {
                throw new SlimException("Option not found.");
            }
            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetNextExpiration(option, direction);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public async Task<Option> GetNextStrikeOption(Option option, PermutationDirection direction)
        {
            if (option == null)
            {
                throw new SlimException("Option not found.");
            }
            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetNextStrike(option, direction);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public async Task<HashSet<double>> GetStrikesSharingExpiration(Option option)
        {
            if (option == null)
            {
                throw new SlimException("Option not found.");
            }
            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetStrikesSharingExpiration(option);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public async Task<HashSet<DateTime>> GetExpirationsSharingStrike(Option option)
        {
            if (option == null)
            {
                throw new SlimException("Option not found.");
            }
            List<Option> optionChain = await GetSymbols(option.UnderlyingSymbol);

            if (optionChain != null && optionChain.Count >= 0)
            {
                return OptionsLookup.GetExpirationsSharingStrike(option);
            }
            else
            {
                throw new SlimException("Option chain not found.");
            }
        }

        public double GetQuoteSnapshot(string symbol, SubscriptionFieldType quoteType)
        {
            double result;
            Task<double> midPriceTask = GetSnapshotAsync(symbol, quoteType);
            midPriceTask.Wait();
            result = midPriceTask.Result;
            return result;
        }

        public async Task<double> GetSnapshotAsync(string symbol, SubscriptionFieldType quoteType)
        {
            try
            {
                if (quoteType == SubscriptionFieldType.PreviousClose)
                {
                    symbol = OptionsHelper.IsIndex(symbol) ? "$" + symbol : symbol;
                    return _marketDataChannel.TryGetQuoteSnapshot(symbol, quoteType, QuoteSource.Databento);
                }
                else if (quoteType is >= SubscriptionFieldType.Delta and <= SubscriptionFieldType.TheorethicalValue)
                {
                    DataStore dataStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                    dataStore.GetHanweckDataFor(symbol, quoteType);
                    return await dataStore.GetDataAsync(symbol);
                }
                else
                {
                    DataStore dataStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                    dataStore.GetQuoteDataFor(symbol, quoteType);
                    return await dataStore.GetDataAsync(symbol);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetSnapshotAsync));
                return double.NaN;
            }
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;

            if (type == SubscriptionFieldType.TronTrade)
            {
                _marketDataChannel.SubscribeTronTrades(symbol);
            }
            else
            {
                if (_activeQuoteSource == QuoteSource.Databento && IsDatabentoType(type))
                {
                    TrackDatabentoSubscription(subscription);
                    _omsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.Mbp1, this);
                    UnsubscribeTron(symbol, type);
                }
                else
                {
                    SubscribeTronData(symbol, type);
                    if (_activeQuoteSource != QuoteSource.Databento && IsDatabentoType(type))
                    {
                        TryReleaseDatabentoSubscription(subscription, out bool unsubscribeDatabento);
                        if (unsubscribeDatabento)
                        {
                            UnsubscribeDatabento(symbol);
                        }
                    }
                }
            }
        }

        public void ThrottleMarketData(bool isPerformanceMode)
        {
            _marketDataChannel.ThrottleMarketData(isPerformanceMode);
        }

        private static bool IsDatabentoType(SubscriptionFieldType type)
        {
            return type is SubscriptionFieldType.Bid or SubscriptionFieldType.Ask or SubscriptionFieldType.BidSize or SubscriptionFieldType.AskSize or SubscriptionFieldType.MidPoint or SubscriptionFieldType.LastPrice;
        }

        private void SubscribeTronData(string symbol, SubscriptionFieldType type, MdsDataSource mdsDataSource = MdsDataSource.None)
        {
            if (CanBeSwapped(symbol, type) && Config.SymbolsLookup.TryGetValue(symbol, out var tuple))
            {
                symbol = tuple.Item2;
            }

            if (string.IsNullOrEmpty(symbol))
            {
                _log.Warn(nameof(SubscribeTronData) + ". Symbol can not be empty. Type: " + type);
                return;
            }

            if (symbol.Contains('\\'))
            {
                _log.Warn(nameof(SubscribeTronData) + $". Unsupported symbol format. Symbol: {symbol}, Type: {type}");
                return;
            }

            if (IsEntitledForData(symbol))
            {
                _marketDataChannel.SubscribeTronMarketData(symbol, type, mdsDataSource);
            }
            else
            {
                ResetSubscription(symbol, type);
            }
        }

        private static bool CanBeSwapped(string symbol, SubscriptionFieldType type)
        {
            return type != SubscriptionFieldType.LastPrice || symbol != "$SPX";
        }

        private bool IsEntitledForData(string symbol)
        {
            if (symbol.StartsWith("."))
            {
                return CheckForValidEntitlement("OPRA");
            }

            if (symbol.StartsWith("/"))
            {
                return CheckForValidEntitlement("CME");
            }

            return CheckForValidEntitlement("CTA") && CheckForValidEntitlement("UTP");
        }

        private void ResetSubscription(string symbol, SubscriptionFieldType type)
        {
            Update(symbol, type, double.NaN);
            Unsubscribe(new(symbol, type));
        }

        private bool CheckForValidEntitlement(string key)
        {
            if (!_omsCore.GatewayClient.EntitlementMap.TryGetValue(key, out var ent) || ent == null)
            {
                return false;
            }

            var validEntitlement = ent.Simultaneous > 0 &&
                                        ent.ActivationTime < DateTime.Now &&
                                        (ent.DeactivationTime < ent.ActivationTime || ent.DeactivationTime > DateTime.Now);
            return validEntitlement;
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            string originalSymbol = subscription.Symbol;
            string symbol = originalSymbol;
            SubscriptionFieldType type = subscription.Type;
            bool useAggr = false;

            if (type == SubscriptionFieldType.TronTrade)
            {
                if (Config.SymbolsLookup.TryGetValue(symbol, out var tuple))
                {
                    symbol = tuple.Item2;
                }
                _marketDataChannel.UnsubscribeTronTrades(symbol);
            }
            else
            {
                if (_activeQuoteSource == QuoteSource.Databento && IsDatabentoType(type))
                {
                    TryReleaseDatabentoSubscription(subscription, out bool unsubscribe);

                    if (unsubscribe)
                    {
                        UnsubscribeDatabento(originalSymbol);
                    }
                }
                else
                {
                    if (Config.SymbolsLookup.TryGetValue(symbol, out var tuple))
                    {
                        symbol = tuple.Item2;
                        useAggr = tuple.Item4;
                    }
                    var aggr = useAggr && type is SubscriptionFieldType.Bid or SubscriptionFieldType.Ask or SubscriptionFieldType.MidPoint;
                    var source = aggr ? MdsDataSource.AggrTron : MdsDataSource.None;
                    UnsubscribeTron(symbol, type, source);
                }
            }
        }

        private void TrackDatabentoSubscription(SubscriptionKey subscription)
        {
            lock (_databentoSubscriptionsLock)
            {
                if (_databentoTrackedQuoteSubscriptions.TryAdd(subscription, byte.MinValue))
                {
                    _databentoSubscriptions.TryGetValue(subscription.Symbol, out var count);
                    _databentoSubscriptions[subscription.Symbol] = ++count;
                }
            }
        }

        private void TryReleaseDatabentoSubscription(SubscriptionKey subscription, out bool unsubscribe)
        {
            unsubscribe = false;
            lock (_databentoSubscriptionsLock)
            {
                if (_databentoTrackedQuoteSubscriptions.TryRemove(subscription, out _) &&
                    _databentoSubscriptions.TryGetValue(subscription.Symbol, out var count))
                {
                    var result = Math.Max(count - 1, 0);
                    if (result == 0)
                    {
                        _databentoSubscriptions.TryRemove(subscription.Symbol, out _);
                    }
                    else
                    {
                        _databentoSubscriptions[subscription.Symbol] = result;
                    }
                    unsubscribe = result == 0;
                }
            }
        }

        private void UnsubscribeDatabento(string symbol)
        {
            _omsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.Mbp1, this);
        }

        private void UnsubscribeTron(string symbol, SubscriptionFieldType type, MdsDataSource mdsDataSource = MdsDataSource.None)
        {
            _marketDataChannel.UnsubscribeTronMarketData(symbol, type, mdsDataSource);
        }
    }
}
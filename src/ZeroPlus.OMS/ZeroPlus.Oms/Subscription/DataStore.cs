using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Subscription
{
    internal enum StoreType
    {
        Quote,
        Hanweck,
        Raptor,
        Ema,
        Vola
    }

    public class DataStore : IOmsDataSubscriber, IDataStore
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private ConcurrentDictionary<string, DataResult> _symbolToResultsMap = new();
        private readonly CancellationToken _token;
        private readonly int _timeout;
        private readonly bool _useGlobalTimeout;
        private SubscriptionFieldType _type;
        private StoreType _storeType;
        private bool _globalWaitElapsed;

        public bool IsDisposed { get; set; }

        private readonly OmsCore _omsCore;
        public bool ApproximateOnFailure { get; set; }

        public DataStore() :
            this(CancellationToken.None, 100, false, false)
        {
            _omsCore = ServiceLocator.GetService<OmsCore>();
        }

        public DataStore(int timeout, bool useGlobalTimeout, bool approximateOnFailure = false) :
            this(CancellationToken.None, timeout, useGlobalTimeout, approximateOnFailure)
        {
            _omsCore = ServiceLocator.GetService<OmsCore>();
        }

        public DataStore(CancellationToken token, int timeout, bool useGlobalTimeout, bool approximateOnFailure = false)
        {
            _omsCore = ServiceLocator.GetService<OmsCore>();
            _token = token;
            _timeout = timeout;
            _useGlobalTimeout = useGlobalTimeout;
            ApproximateOnFailure = approximateOnFailure;
        }

        public void GetQuoteDataFor(List<Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Quote;
            _type = type;
            foreach (Option symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.Symbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    _omsCore.QuoteClient.Subscribe(optionSymbol, type, this);
                }
            }
        }

        public void GetQuoteDataFor(string symbol, SubscriptionFieldType type)
        {
            _storeType = StoreType.Quote;
            _type = type;
            string optionSymbol = symbol;
            if (!_symbolToResultsMap.ContainsKey(optionSymbol))
            {
                _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                _omsCore.QuoteClient.Subscribe(optionSymbol, type, this);
            }
        }

        public void GetEmaDataFor(List<Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Ema;
            _type = type;
            foreach (Option symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.Symbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    _omsCore.UpdateManager.Subscribe(optionSymbol, type, this);
                }
            }
        }

        public void GetEmaDataFor(string symbol, SubscriptionFieldType type)
        {
            _storeType = StoreType.Ema;
            _type = type;
            string optionSymbol = symbol;
            if (!_symbolToResultsMap.ContainsKey(optionSymbol))
            {
                _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                _omsCore.UpdateManager.Subscribe(optionSymbol, type, this);
            }
        }

        public void GetHanweckDataFor(List<Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Hanweck;
            _type = type;
            foreach (Option symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.Symbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    if (_omsCore.GreekClient.IsConnected)
                    {
                        _omsCore.GreekClient.Subscribe(optionSymbol, type, this);
                    }
                }
            }
        }

        public void GetHanweckDataFor(List<Oms.Data.Securities.Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Hanweck;
            _type = type;
            foreach (var symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.OptionSymbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    if (_omsCore.GreekClient.IsConnected)
                    {
                        _omsCore.GreekClient.Subscribe(optionSymbol, type, this);
                    }
                }
            }
        }

        public void GetHanweckDataFor(string symbol, SubscriptionFieldType type)
        {
            _storeType = StoreType.Hanweck;
            _type = type;
            string optionSymbol = symbol;
            if (!_symbolToResultsMap.ContainsKey(optionSymbol))
            {
                _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                if (_omsCore.GreekClient.IsConnected)
                {
                    _omsCore.GreekClient.Subscribe(optionSymbol, type, this);
                }
            }
        }

        public void GetRaptorDataFor(string symbol, SubscriptionFieldType type)
        {
            _storeType = StoreType.Raptor;
            _type = type;
            string optionSymbol = symbol;
            if (!_symbolToResultsMap.ContainsKey(optionSymbol))
            {
                _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                _omsCore.UpdateManager.Subscribe(optionSymbol, type, this);
            }
        }

        public void GetRaptorDataFor(List<Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Raptor;
            _type = type;
            foreach (Option symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.Symbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    _omsCore.UpdateManager.Subscribe(optionSymbol, type, this);
                }
            }
        }

        public void GetVolaDataFor(string symbol, SubscriptionFieldType type)
        {
            _storeType = StoreType.Vola;
            _type = type;
            string optionSymbol = symbol;
            if (!_symbolToResultsMap.ContainsKey(optionSymbol))
            {
                _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                _omsCore.UpdateManager.Subscribe(optionSymbol, SubscriptionFieldType.DeltaAdjTheo, this);
            }
        }

        public void GetVolaDataFor(List<Option> optionChain, SubscriptionFieldType type)
        {
            _storeType = StoreType.Vola;
            _type = type;
            foreach (Option symbol in optionChain)
            {
                _token.ThrowIfCancellationRequested();
                string optionSymbol = symbol.Symbol;
                if (!_symbolToResultsMap.ContainsKey(optionSymbol))
                {
                    _symbolToResultsMap[optionSymbol] = new DataResult(_token, _timeout, !_useGlobalTimeout);
                    _omsCore.UpdateManager.Subscribe(optionSymbol, SubscriptionFieldType.DeltaAdjTheo, this);
                }
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string optionSymbol = key.Symbol;
            SubscriptionFieldType type = key.Type;

            if (_symbolToResultsMap.TryGetValue(optionSymbol, out DataResult result))
            {
                if (value is double valueDouble)
                {
                    result.Value = valueDouble;
                }
                else if (value is EmaUpdateModel emaUpdate)
                {
                    result.Value = emaUpdate.MidPeriodEmaAdj;
                }
                else if (value is DeltaAdjTheo theoUpdate)
                {
                    switch (_storeType)
                    {
                        case StoreType.Raptor:
                            result.Value = theoUpdate.DeltaAdjustedTheo;
                            break;
                        case StoreType.Vola:
                            result.Value = theoUpdate.SecondaryTheoAdj;
                            break;
                    }
                }
            }

            switch (_storeType)
            {
                case StoreType.Quote:
                    _omsCore.QuoteClient.Unsubscribe(optionSymbol, type, this);
                    break;
                case StoreType.Hanweck:
                    _omsCore.GreekClient.Unsubscribe(optionSymbol, type, this);
                    break;
                case StoreType.Ema:
                case StoreType.Raptor:
                case StoreType.Vola:
                    _omsCore.UpdateManager.Unsubscribe(optionSymbol, type, this);
                    break;
            }
        }

        public async ValueTask<double> GetDataAsync(string optionSymbol, bool approximateOnFailure = true)
        {
            double returnVal = double.NaN;
            if (_symbolToResultsMap.TryGetValue(optionSymbol, out DataResult dataValue))
            {
                if (!dataValue.ValueReceived())
                {
                    if (_useGlobalTimeout)
                    {
                        if (!_globalWaitElapsed)
                        {
                            await Task.Delay(_timeout, _token);
                            _globalWaitElapsed = true;
                        }
                    }
                    else
                    {
                        await dataValue.WaitForValueAsync();
                    }
                }
                returnVal = dataValue.Value;
            }
            if (double.IsNaN(returnVal))
            {
                _log.Info($"{nameof(GetDataAsync)} -> Waiting for data timed out for {optionSymbol}");
                if (ApproximateOnFailure && approximateOnFailure)
                {
                    returnVal = await GetApproximateValue(optionSymbol);
                }
            }

            return returnVal;
        }

        public void Reset()
        {
            _symbolToResultsMap.Clear();
        }

        private async Task<double> GetApproximateValue(string optionSymbol)
        {
            double returnVal = await GetApproximateStrikeValue(optionSymbol);
            if (double.IsNaN(returnVal))
            {
                returnVal = await GetApproximateExpirationValue(optionSymbol);
            }

            return returnVal;
        }

        private async Task<double> GetApproximateStrikeValue(string optionSymbol)
        {
            double returnValue;
            try
            {
                double prevValue = await GetNextStrikeValue(optionSymbol, PermutationDirection.Down);
                double nextValue = await GetNextStrikeValue(optionSymbol, PermutationDirection.Up);
                returnValue = (prevValue + nextValue) / 2;
            }
            catch (Exception)
            {
                _log.Error($"{nameof(GetApproximateStrikeValue)} -> Value approximation failed for {optionSymbol}");
                returnValue = double.NaN;
            }
            return returnValue;
        }

        private async Task<double> GetApproximateExpirationValue(string optionSymbol)
        {
            double returnVal;
            try
            {
                double prevValue = await GetNextExpirationValue(optionSymbol, PermutationDirection.Down);
                double nextValue = await GetNextExpirationValue(optionSymbol, PermutationDirection.Up);
                returnVal = (prevValue + nextValue) / 2;
            }
            catch (Exception)
            {
                _log.Error($"{nameof(GetApproximateExpirationValue)} -> Value approximation failed for {optionSymbol}");
                returnVal = double.NaN;
            }
            return returnVal;
        }

        private async Task<double> GetNextStrikeValue(string optionSymbol, PermutationDirection direction)
        {
            DataStore backupStore = new(_token, _timeout, !_useGlobalTimeout, _useGlobalTimeout);
            try
            {
                Option prevExp = _omsCore.SecurityBook.GetSecurity((await _omsCore.QuoteClient.GetNextStrikeOption(optionSymbol, direction)).OptionSymbol) as Option;
                double nextStrikeValue;
                while (true)
                {
                    if (prevExp == null)
                    {
                        continue;
                    }
                    if (_symbolToResultsMap.ContainsKey(prevExp.Symbol))
                    {
                        nextStrikeValue = await GetDataAsync(prevExp.Symbol, approximateOnFailure: false);
                    }
                    else
                    {
                        switch (_storeType)
                        {
                            case StoreType.Quote:
                                backupStore.GetQuoteDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Hanweck:
                                backupStore.GetHanweckDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Raptor:
                                backupStore.GetRaptorDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Ema:
                                backupStore.GetEmaDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Vola:
                                backupStore.GetVolaDataFor(prevExp.Symbol, _type);
                                break;
                        }
                        nextStrikeValue = await backupStore.GetDataAsync(prevExp.Symbol, approximateOnFailure: false);
                    }

                    if (double.IsNaN(nextStrikeValue))
                    {
                        prevExp = _omsCore.SecurityBook.GetSecurity((await _omsCore.QuoteClient.GetNextStrikeOption(prevExp.Symbol, direction)).OptionSymbol) as Option;
                    }
                    else
                    {
                        break;
                    }
                }
                return nextStrikeValue;
            }
            finally
            {
                backupStore.Dispose();
            }
        }

        private async Task<double> GetNextExpirationValue(string optionSymbol, PermutationDirection direction)
        {
            DataStore backupStore = new(_token, _timeout, !_useGlobalTimeout, _useGlobalTimeout);
            try
            {
                Option prevExp = _omsCore.SecurityBook.GetSecurity((await _omsCore.QuoteClient.GetNextExpirationOption(optionSymbol, direction)).OptionSymbol) as Option;
                double nextExpValue;
                while (true)
                {
                    if (prevExp == null)
                    {
                        continue;
                    }
                    if (_symbolToResultsMap.ContainsKey(prevExp.Symbol))
                    {
                        nextExpValue = await GetDataAsync(prevExp.Symbol, approximateOnFailure: false);
                    }
                    else
                    {
                        switch (_storeType)
                        {
                            case StoreType.Quote:
                                backupStore.GetQuoteDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Hanweck:
                                backupStore.GetHanweckDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Raptor:
                                backupStore.GetRaptorDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Ema:
                                backupStore.GetEmaDataFor(prevExp.Symbol, _type);
                                break;
                            case StoreType.Vola:
                                backupStore.GetVolaDataFor(prevExp.Symbol, _type);
                                break;
                        }
                        nextExpValue = await backupStore.GetDataAsync(prevExp.Symbol, approximateOnFailure: false);
                    }

                    if (double.IsNaN(nextExpValue))
                    {
                        prevExp = _omsCore.SecurityBook.GetSecurity((await _omsCore.QuoteClient.GetNextExpirationOption(prevExp.Symbol, direction)).OptionSymbol) as Option;
                    }
                    else
                    {
                        break;
                    }
                }

                return nextExpValue;
            }
            finally
            {
                backupStore.Dispose();
            }
        }

        public void Dispose()
        {
            _omsCore.QuoteClient.UnsubscribeAll(this);
            _omsCore.GreekClient.UnsubscribeAll(this);
            _omsCore.UpdateManager.UnsubscribeAll(this);
            _symbolToResultsMap.Clear();
            _symbolToResultsMap = null;
            IsDisposed = true;
        }
    }
}

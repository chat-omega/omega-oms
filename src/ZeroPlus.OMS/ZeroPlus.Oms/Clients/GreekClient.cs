using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Clients
{

    public class GreekClient : SubscriptionProvider, IOmsDataSubscriber
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly CommsClient _commsClient;
        private bool _isConnected;
        private readonly SubscriptionFieldType[] _greeks = { SubscriptionFieldType.Delta,
                                        SubscriptionFieldType.Gamma,
                                        SubscriptionFieldType.Vega,
                                        SubscriptionFieldType.Theta,
                                        SubscriptionFieldType.Rho,
                                        SubscriptionFieldType.ImpliedVol,
                                        SubscriptionFieldType.TheorethicalValue,
                                        SubscriptionFieldType.GreekTimeStamp,
                                        SubscriptionFieldType.GreekUnder,
                                        SubscriptionFieldType.GreekUnderBid,
                                        SubscriptionFieldType.GreekUnderAsk,
                                        SubscriptionFieldType.GreekBid,
                                        SubscriptionFieldType.GreekAsk,
                                        SubscriptionFieldType.GreekBidTimeStamp,
                                        SubscriptionFieldType.GreekAskTimeStamp,
                                        SubscriptionFieldType.Greeks };

        private readonly OmsCore _omsCore;

        public bool IsDisposed { get; set; }
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public GreekClient(OmsConfig config, OmsCore omsCore)
        {
            Config = config;
            _omsCore = omsCore;
            _commsClient = new CommsClient(OmsConfig.HwGuid, config, HandleMessage, omsCore);
            _commsClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
        }

        #region PublicMethods
        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            return await Task.Run(() => _commsClient.Start(Config.HanweckAddress, Config.HanweckPort));
        }

        public async Task StopAsync()
        {
            await Task.Run(() => _commsClient.Stop());
        }
        #endregion

        private void OnConnectionStatusChangedEvent(bool connected)
        {
            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (connected)
            {
                Resubscribe();
            }
        }

        private void HandleMessage(Message message)
        {
            switch (message.Template.TemplateType)
            {
                case TemplateType.MDSendGreekData:
                    MDSendGreekData greekMessage = MessageFactory.DecodeMDSendGreekDataMessage(message);
                    HandleGreekMessage(greekMessage);
                    break;
                case TemplateType.HWSendGreekData:
                    HWSendGreekData hwGreekMessage = MessageFactory.DecodeHWSendGreekDataMessage(message);
                    HandleHanweekGreekMessage(hwGreekMessage);
                    break;
                default:
                    break;
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
                _log.Error(ex, $"{nameof(HandleGreekMessage)}");
            }
        }

        private void HandleHanweekGreekMessage(HWSendGreekData hwGreekMessage)
        {
            try
            {
                if (hwGreekMessage.GreekDataList.Count > 0)
                {
                    foreach (HWGreekData greekMessage in hwGreekMessage.GreekDataList)
                    {
                        SubscriptionKey subscriptionKey = new(greekMessage.Symbol, SubscriptionFieldType.Greeks);
                        if (TryGetSubscribers(subscriptionKey, out var subscribers))
                        {
                            GreekUpdate greekUpdate = new()
                            {
                                Delta = greekMessage.Delta,
                                Gamma = greekMessage.Gamma,
                                Vega = greekMessage.Vega,
                                Theta = greekMessage.Theta,
                                Rho = greekMessage.Rho,
                                Implied = greekMessage.IV,
                                Theo = greekMessage.TV,
                                HanweckTime = greekMessage.TimeStamp.ToString("hh:mm:ss.fff"),
                                HanweckTimeRaw = greekMessage.TimeStamp,
                                InfoBits = greekMessage.InfoBits,
                                TimeValue = greekMessage.TimeValue,
                                IntrinsicValue = greekMessage.IntrinsicValue,
                                FVDivs = greekMessage.FVDivs,
                                UPrice = greekMessage.UPrice,
                                UTheo = greekMessage.UTheo,
                                UFwd = greekMessage.UFwd,
                                UFwdFactor = greekMessage.UFwdFactor,
                                BorrowCost = greekMessage.BorrowCost,
                                BorrowRate = greekMessage.BorrowRate,
                            };

                            subscribers.UpdateValues(greekUpdate);
                        }

                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.Delta, greekMessage.Delta);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.Gamma, greekMessage.Gamma);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.Vega, greekMessage.Vega);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.Theta, greekMessage.Theta);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.Rho, greekMessage.Rho);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.ImpliedVol, greekMessage.IV);
                        UpdateSubscriptions(greekMessage.Symbol, SubscriptionFieldType.TheorethicalValue, greekMessage.TV);
                    }
                }
                else
                {
                    SubscriptionKey subscriptionKey = new(hwGreekMessage.Symbol, SubscriptionFieldType.Greeks);
                    if (TryGetSubscribers(subscriptionKey, out var subscribers))
                    {
                        GreekUpdate greekUpdate = new()
                        {
                            Delta = hwGreekMessage.Delta,
                            Gamma = hwGreekMessage.Gamma,
                            Vega = hwGreekMessage.Vega,
                            Theta = hwGreekMessage.Theta,
                            Rho = hwGreekMessage.Rho,
                            Implied = hwGreekMessage.IV,
                            Theo = hwGreekMessage.TV,
                        };

                        subscribers.UpdateValues(greekUpdate);
                    }

                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.Delta, hwGreekMessage.Delta);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.Gamma, hwGreekMessage.Gamma);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.Vega, hwGreekMessage.Vega);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.Theta, hwGreekMessage.Theta);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.Rho, hwGreekMessage.Rho);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.ImpliedVol, hwGreekMessage.IV);
                    UpdateSubscriptions(hwGreekMessage.Symbol, SubscriptionFieldType.TheorethicalValue, hwGreekMessage.TV);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(HandleHanweekGreekMessage)}");
            }
        }

        private void UpdateSubscriptions(string symbol, SubscriptionFieldType quoteType, double value)
        {
            SubscriptionKey subscriptionKey = new(symbol, quoteType);
            if (TryGetSubscribers(subscriptionKey, out var subscribers))
            {
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    subscribers.UpdateValues(value);
                }
            }
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;

            if (string.IsNullOrEmpty(symbol))
            {
                _log.Warn(nameof(Subscribe) + ". Symbol can not be empty. Type: " + type);
                return;
            }

            switch (OmsCore.Config.GreeksSource)
            {
                case GreekSource.Hanweck:
                    _commsClient.SubscribeHanweckData(symbol, type);
                    break;
                case GreekSource.ZeroPlus:
                    _omsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.VolaGreeks, this);
                    break;
            }
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;

            foreach (SubscriptionFieldType greekType in _greeks)
            {
                if (greekType == type)
                {
                    continue;
                }

                SubscriptionKey subscriptionKey = new(symbol, greekType);
                if (TryGetSubscribers(subscriptionKey, out var subscribers))
                {
                    if (!subscribers.IsEmpty())
                    {
                        return;
                    }
                }
            }

            switch (OmsCore.Config.GreeksSource)
            {
                case GreekSource.Hanweck:
                    _commsClient.UnsubscribeHanweckData(symbol, type);
                    break;
                case GreekSource.ZeroPlus:
                    _omsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.VolaGreeks, this);
                    break;
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
        {
            if (key.Type == SubscriptionFieldType.VolaGreeks && value is SlimGreekUpdateModel greekMessage && OmsCore.Config.GreeksSource == GreekSource.ZeroPlus)
            {
                SubscriptionKey subscriptionKey = new(key.Symbol, SubscriptionFieldType.Greeks);
                if (TryGetSubscribers(subscriptionKey, out var subscribers))
                {
                    GreekUpdate greekUpdate = new()
                    {
                        Delta = greekMessage.Delta,
                        Gamma = greekMessage.Gamma,
                        Vega = greekMessage.Vega,
                        Theta = double.NaN,
                        Rho = double.NaN,
                        Implied = greekMessage.Vol,
                        Theo = greekMessage.Theo,
                        HanweckTime = greekMessage.TimeStamp.FromUnixEpoch().ToString("hh:mm:ss.fff"),
                        HanweckTimeRaw = greekMessage.TimeStamp.FromUnixEpoch(),
                    };

                    subscribers.UpdateValues(greekUpdate);
                }

                UpdateSubscriptions(key.Symbol, SubscriptionFieldType.Delta, greekMessage.Delta);
                UpdateSubscriptions(key.Symbol, SubscriptionFieldType.Gamma, greekMessage.Gamma);
                UpdateSubscriptions(key.Symbol, SubscriptionFieldType.Vega, greekMessage.Vega);
                UpdateSubscriptions(key.Symbol, SubscriptionFieldType.ImpliedVol, greekMessage.Vol);
                UpdateSubscriptions(key.Symbol, SubscriptionFieldType.TheorethicalValue, greekMessage.Theo);
            }
        }
    }
}
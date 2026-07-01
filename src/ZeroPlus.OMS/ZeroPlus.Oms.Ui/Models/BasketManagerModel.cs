using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.BasketManager;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Managers;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketManagerModel : BindableBase, IOmsDataSubscriber
    {
        private readonly int STALE_SERVER_TIME_THRESHOLD = 10;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, BasketModel> _basketIdToBasketMap = new();
        private string _lastServerTime = "";
        private int _lastServerTimeSame = 0;
        readonly PortfolioManagerModel _portfolioManager;
        public bool _AlertCreepEnabled;
        public bool _StopCreepEnabled;
        public double _AlertCreepThreshold;
        public double _StopCreepThreshold;

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool IsDisposed { get; set; }

        public ObservableCollection<BasketModel> Baskets { get; set; } = new ObservableCollection<BasketModel>();

        public ObservableCollection<BasketTradeUpdate> Trades { get; set; } = new ObservableCollection<BasketTradeUpdate>();

        public double ServerCreep => Math.Round(OmsCore.QuoteClient.ServerCreepMs / 1000.0, 3, MidpointRounding.AwayFromZero);

        [Bindable]
        public partial bool AlertCreepEnabled { get; set; }
        [Bindable]
        public partial bool StopCreepEnabled { get; set; }
        [Bindable]
        public partial double AlertCreepThreshold { get; set; }
        [Bindable]
        public partial double StopCreepThreshold { get; set; }

        public BasketManagerModel(PortfolioManagerModel portfolioManagerModel)
        {
            _portfolioManager = portfolioManagerModel;
            AlertCreepThreshold = 5;
            StopCreepThreshold = 10;
            OmsCore.QuoteClient.Subscribe(String.Empty, SubscriptionFieldType.ServerClockUpdate, this);
            OmsCore.BasketManager.BasketUpdatedEvent += BasketManager_BasketUpdatedEvent;
            OmsCore.BasketManager.BasketDisconnectedEvent += BasketManager_BasketDisconnectedEvent;
            OmsCore.BasketManager.BasketTradeUpdateEvent += BasketManager_BasketTradeUpdateEvent;
            OmsCore.BasketManager.ServerStatusChangedEvent += DominatorsManager_ServerStatusChangedEvent;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.ServerClockUpdate &&
                     value is DateTime clockUpdate)
            {
                string clockUpdateString = clockUpdate.ToString("hh:mm:ss.fff");
                if (_lastServerTime == clockUpdateString)
                {
                    _lastServerTimeSame++;
                }
                else
                {
                    _lastServerTime = clockUpdateString;
                    _lastServerTimeSame = 0;
                }

                if (_lastServerTimeSame > STALE_SERVER_TIME_THRESHOLD)
                {
                    _log.Info("Stale Server Time Threshold Passed.");
                }

                if (StopCreepEnabled && ServerCreep > StopCreepThreshold)
                {
                    _log.Info("Stop Creep Threshold Passed. All basket orders cancelled.");
                }

                if (AlertCreepEnabled && ServerCreep > AlertCreepThreshold)
                {
                    _log.Info("Alert Creep Threshold Passed.");
                }
            }
        }

        private void DominatorsManager_ServerStatusChangedEvent(bool serverUp)
        {
            _basketIdToBasketMap.Clear();
        }

        private void BasketManager_BasketUpdatedEvent(IBasket basket)
        {
            if (!_basketIdToBasketMap.TryGetValue(basket.Uid, out BasketModel model))
            {
                model = new BasketModel(basket, _portfolioManager);
                _basketIdToBasketMap[basket.Uid] = model;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Baskets.Add(model);
                }));
            }
            model.Update(basket);
        }

        private void BasketManager_BasketDisconnectedEvent(IBasket basket)
        {
            if (_basketIdToBasketMap.TryRemove(basket.Uid, out BasketModel model))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Baskets.Remove(model);
                    model.Dispose();
                }));
            }
        }

        private void BasketManager_BasketTradeUpdateEvent(BasketTradeUpdate basketTradeUpdate)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Trades.Add(basketTradeUpdate);
            }));
        }

        private void CancellAll()
        {
            try
            {
                Parallel.ForEach(Baskets.ToList(), basket =>
                {
                    _ = basket.CancelAllAsync();
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancellAll));
            }
        }
    }
}
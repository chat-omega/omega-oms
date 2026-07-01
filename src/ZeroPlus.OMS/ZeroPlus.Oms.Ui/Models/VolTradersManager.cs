using DevExpress.Mvvm;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void VolTraderUpdatedEventHandler(VolTraderViewModel model, bool added);
    public partial class VolTradersManager : BindableBase
    {
        public event VolTraderUpdatedEventHandler VolTraderUpdatedEvent;
        private readonly object _lock;
        private readonly ConcurrentDictionary<BasketTraderViewModel, HashSet<VolTraderViewModel>> _basketToVolTradersMap;
        private readonly ConcurrentDictionary<VolTraderViewModel, HashSet<BasketTraderViewModel>> _volTraderToBasketsMap;

        [Bindable]
        public partial List<VolTraderViewModel> VolTraders { get; set; }

        public VolTradersManager()
        {
            _lock = new object();
            _volTraderToBasketsMap = new ConcurrentDictionary<VolTraderViewModel, HashSet<BasketTraderViewModel>>();
            _basketToVolTradersMap = new ConcurrentDictionary<BasketTraderViewModel, HashSet<VolTraderViewModel>>();
            VolTraders = [];
        }

        internal void Add(VolTraderViewModel volTraderViewModel)
        {
            VolTraders.Add(volTraderViewModel);
            VolTraderUpdatedEvent?.Invoke(volTraderViewModel, true);
        }

        internal void Remove(VolTraderViewModel volTraderViewModel)
        {
            VolTraders.Remove(volTraderViewModel);
            bool found = false;
            HashSet<BasketTraderViewModel> baskets;
            lock (_lock)
            {
                found = _volTraderToBasketsMap.TryRemove(volTraderViewModel, out baskets);
            }
            if (found)
            {
                foreach (BasketTraderViewModel basket in baskets)
                {
                    RemoveBasketFromVolTraders(basket, volTraderViewModel);
                }
            }
            VolTraderUpdatedEvent?.Invoke(volTraderViewModel, false);
        }

        internal void AddBasketToVolTrader(BasketTraderViewModel basketTraderViewModel, VolTraderViewModel volTrader)
        {
            HashSet<VolTraderViewModel> volTraders;
            HashSet<BasketTraderViewModel> baskets;

            basketTraderViewModel.SetTitle();

            lock (_lock)
            {
                if (!_basketToVolTradersMap.TryGetValue(basketTraderViewModel, out volTraders))
                {
                    volTraders = new HashSet<VolTraderViewModel>();
                    _basketToVolTradersMap[basketTraderViewModel] = volTraders;
                }
                if (!_volTraderToBasketsMap.TryGetValue(volTrader, out baskets))
                {
                    baskets = new HashSet<BasketTraderViewModel>();
                    _volTraderToBasketsMap[volTrader] = baskets;
                }
            }
            volTraders.Add(volTrader);
            volTrader.AddBasket(basketTraderViewModel);
            baskets.Add(basketTraderViewModel);
            basketTraderViewModel.Dispatcher.BeginInvoke(() => basketTraderViewModel.ModuleType = OrderSubType.VolTrader);
        }

        internal void RemoveBasketFromVolTraders(BasketTraderViewModel basketTraderViewModel)
        {
            bool found = false;
            HashSet<VolTraderViewModel> volTraders;
            lock (_lock)
            {
                found = _basketToVolTradersMap.TryRemove(basketTraderViewModel, out volTraders);
            }
            if (found)
            {
                foreach (VolTraderViewModel volTrader in volTraders)
                {
                    volTrader.RemoveBasketNoPrompt(basketTraderViewModel);
                }
            }
            basketTraderViewModel.Dispatcher.BeginInvoke(() => basketTraderViewModel.ModuleType = OrderSubType.Basket);
        }

        internal void RemoveBasketFromVolTraders(BasketTraderViewModel basketTraderViewModel, VolTraderViewModel volTrader)
        {
            bool found = false;
            HashSet<VolTraderViewModel> volTraders;
            lock (_lock)
            {
                found = _basketToVolTradersMap.TryGetValue(basketTraderViewModel, out volTraders);
            }
            if (found)
            {
                volTraders.Remove(volTrader);
                volTrader.RemoveBasketNoPrompt(basketTraderViewModel);
            }
            basketTraderViewModel.Dispatcher.BeginInvoke(() => basketTraderViewModel.ModuleType = OrderSubType.Basket);
        }

        internal bool BasketIsPartOfVolTrader(BasketTraderViewModel basketTraderViewModel)
        {
            bool found = _basketToVolTradersMap.TryGetValue(basketTraderViewModel, out HashSet<VolTraderViewModel> volTraders);
            if (!found)
            {
                return false;
            }
            else
            {
                return volTraders.Count > 0;
            }
        }
    }
}
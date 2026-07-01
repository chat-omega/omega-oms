using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void BasketGroupAddedEventHandler(BasketGroupModel basketGroupModel);
    public delegate void BasketGroupRemovedEventHandler(BasketGroupModel basketGroupModel);

    public class BasketGroupManagerModel
    {
        public event BasketGroupAddedEventHandler BasketGroupAddedEvent;
        public event BasketGroupRemovedEventHandler BasketGroupRemovedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private int _nextBasketGroupCounter;
        private readonly ConcurrentDictionary<Tuple<string, string>, HashSet<BasketTraderViewModel>> _idToBasketGroups = new();
        private readonly ConcurrentDictionary<Tuple<string, string>, BasketTraderView> _idToCobBasketMap = new();
        readonly DominatorsManagerModel _dominatorsManagerModel;

        public IEnumerable<BasketGroupModel> GetAllGroups => _idToBasketGroups.Select(x => new BasketGroupModel(x.Key.Item1, x.Key.Item2));
        public List<Tuple<string, BasketTraderViewModel>> AllBaskets { get; private set; } = new List<Tuple<string, BasketTraderViewModel>>();
        public OmsCore OmsCore { get; }

        public BasketGroupManagerModel(DominatorsManagerModel dominatorsManagerModel, OmsCore omsCore)
        {
            _dominatorsManagerModel = dominatorsManagerModel;
            OmsCore = omsCore;
        }

        internal BasketGroupModel GetNextGroup(BasketTraderViewModel basketTraderViewModel)
        {
            BasketGroupModel basketGroupModel = new()
            {
                Name = "Basket Group " + ++_nextBasketGroupCounter,
                Uid = basketTraderViewModel.BasketSettings.Uid,
            };
            Tuple<string, string> key = Tuple.Create(basketGroupModel.Name, basketGroupModel.Uid);
            _idToBasketGroups[key] = new HashSet<BasketTraderViewModel>();
            AddToGroup(basketGroupModel, basketTraderViewModel);
            BasketGroupAddedEvent?.Invoke(basketGroupModel);
            return basketGroupModel;
        }

        internal void AddToGroup(BasketGroupModel selectedBasketGroup, BasketTraderViewModel basketTraderViewModel)
        {

            Tuple<string, string> key = Tuple.Create(selectedBasketGroup.Name, selectedBasketGroup.Uid);
            if (!_idToBasketGroups.TryGetValue(key, out HashSet<BasketTraderViewModel> basketGroup))
            {
                basketGroup = new HashSet<BasketTraderViewModel>();
                _idToBasketGroups[key] = basketGroup;
            }
            basketGroup.Add(basketTraderViewModel);
            foreach (Tuple<string, BasketTraderViewModel> item in AllBaskets.ToList())
            {
                if (item.Item2 == basketTraderViewModel)
                {
                    AllBaskets.Remove(item);
                }
            }
            AllBaskets.Add(Tuple.Create(basketTraderViewModel.ModuleTitle, basketTraderViewModel));

            foreach (Tuple<string, string> item in _idToBasketGroups.Keys)
            {
                HashSet<BasketTraderViewModel> value = _idToBasketGroups[item];
                if (item.Item2 != selectedBasketGroup.Uid)
                {
                    if (value.Contains(basketTraderViewModel))
                    {
                        value.Remove(basketTraderViewModel);
                    }
                    if (value.Count == 0)
                    {
                        _idToBasketGroups.Remove(item, out _);
                        BasketGroupRemovedEvent?.Invoke(new BasketGroupModel(item.Item1, item.Item2));
                    }
                }
            }
        }

        internal void RemoveFromBasketGroups(BasketTraderViewModel basketTraderViewModel)
        {
            foreach (Tuple<string, string> key in _idToBasketGroups.Keys)
            {
                HashSet<BasketTraderViewModel> value = _idToBasketGroups[key];
                if (value.Contains(basketTraderViewModel))
                {
                    value.Remove(basketTraderViewModel);
                }
                if (value.Count == 0)
                {
                    _idToBasketGroups.Remove(key, out _);
                    BasketGroupRemovedEvent?.Invoke(new BasketGroupModel(key.Item1, key.Item2));
                }
                foreach (Tuple<string, BasketTraderViewModel> item in AllBaskets.ToList())
                {
                    if (item.Item2 == basketTraderViewModel)
                    {
                        AllBaskets.Remove(item);
                    }
                }
            }
        }

        internal async Task<int> GetRestingOrdersCount(BasketGroupModel selectedBasketGroup)
        {
            if (selectedBasketGroup == null)
            {
                return 0;
            }
            Tuple<string, string> key = Tuple.Create(selectedBasketGroup.Name, selectedBasketGroup.Uid);
            if (_idToBasketGroups.TryGetValue(key, out HashSet<BasketTraderViewModel> baskets))
            {
                int sum = 0;
                foreach (BasketTraderViewModel basket in baskets)
                {
                    sum += await basket.GetRestingOrdersCountSafe();
                }
                return sum;
            }
            else
            {
                return 0;
            }
        }

        internal void NotifyOrderClose(BasketGroupModel selectedBasketGroup)
        {
            if (selectedBasketGroup == null)
            {
                return;
            }
            Tuple<string, string> key = Tuple.Create(selectedBasketGroup.Name, selectedBasketGroup.Uid);
            if (_idToBasketGroups.TryGetValue(key, out HashSet<BasketTraderViewModel> baskets))
            {
                foreach (BasketTraderViewModel basket in baskets)
                {
                    basket.NotifyOrderCloseListenersSafe();
                }
            }
        }

        internal void CancelQueuedOrders(BasketGroupModel selectedBasketGroup)
        {
            if (selectedBasketGroup == null)
            {
                return;
            }
            Tuple<string, string> key = Tuple.Create(selectedBasketGroup.Name, selectedBasketGroup.Uid);
            if (_idToBasketGroups.TryGetValue(key, out HashSet<BasketTraderViewModel> baskets))
            {
                foreach (BasketTraderViewModel basket in baskets)
                {
                    basket.CancelQueuedOrdersSafe();
                }
            }
        }

        internal List<(string Underlying, string SpreadId, int RequiredStocks)> GetHedgeDeltaItems(BasketGroupModel selectedBasketGroup)
        {
            if (selectedBasketGroup != null)
            {
                Tuple<string, string> key = Tuple.Create(selectedBasketGroup.Name, selectedBasketGroup.Uid);
                if (_idToBasketGroups.TryGetValue(key, out HashSet<BasketTraderViewModel> baskets))
                {
                    return baskets.SelectMany(basket => basket.GetHedgeDeltaItems()).DistinctBy(x => x.SpreadId).ToList();
                }
            }
            return new List<(string Underlying, string SpreadId, int RequiredStocks)>();
        }
    }
}

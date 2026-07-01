using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DevExpress.Mvvm;
using ZeroPlus.Comms.Helper.Concurrency;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Models;
using PositionModel = ZeroPlus.Oms.Ui.Models.PositionModel;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PositionUpdateConsumer : BindableBase, IPositionUpdateSubscriber
    {
        public event BlockUiEventHandler BlockUiEvent;
        public event UnblockUiEventHandler UnblockUiEvent;

        private const int UPDATE_LIMIT = 10_000;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ProducerConsumer _updatedPositionsQueue;
        private readonly object _positionInsertLock = new();
        private readonly HashSet<int> _addedPositionsKey = new();
        private readonly List<PositionModel> _addedPositions = new();
        private readonly ConcurrentDictionary<string, ObservableCollection<PositionModel>> _accountToPositionsCollectionMap = new();
        private readonly string _allAccountsKey;
        private readonly ObservableCollection<PositionModel> _allAccountsPositionCollection;
        private readonly ConcurrentDictionary<Tuple<string, string>, PositionModel> _positionIdToPositionModelMap = new();
        private readonly ManualResetEventSlim _loadingBufferResetEvent = new(true);

        private DispatcherTimer _uiUpdateTimer;

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public PortfolioMarketDataSubscriptionMode[] MarketDataSubscriptionModes { get; } = (PortfolioMarketDataSubscriptionMode[])Enum.GetValues(typeof(PortfolioMarketDataSubscriptionMode));

        public int OpenedPortfolioWindowsCount { get; set; }
        public ObservableCollection<string> Accounts { get; } = new();
        [Bindable]
        public partial PortfolioMarketDataSubscriptionMode MarketDataSubscriptionMode { get; set; }

        public PositionUpdateConsumer()
        {
            _allAccountsKey = "<All>";
            _allAccountsPositionCollection = new ObservableCollection<PositionModel>();
            Accounts.Add(_allAccountsKey);
            _accountToPositionsCollectionMap[_allAccountsKey] = _allAccountsPositionCollection;

            _updatedPositionsQueue = new ProducerConsumer();
            var positionUpdateProcessorThread = new Thread(PositionUpdateProcessHandler)
            {
                IsBackground = true,
            };
            positionUpdateProcessorThread.Start();
            OmsCore.OrderClient.PositionConnectionStatusChangedEvent += OnConnectionStatusChange;
            OmsCore.OrderClient.SetPositionUpdateSubscriber(this);

            StartUiUpdateTimer();
        }

        private void StartUiUpdateTimer()
        {
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(750),
            };
            _uiUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _uiUpdateTimer.Tick += (_, _) => UpdateUiProperties();
            _uiUpdateTimer.Start();
        }

        private void UpdateUiProperties()
        {
            try
            {
                for (int i = _addedPositions.Count - 1; i >= 0; i--)
                {
                    PositionModel item = _addedPositions[i];
                    item.UpdateUiProperties();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateUiProperties));
            }
        }

        public void AddMultipleUpdatedPosition(IEnumerable<OmsPosition> positions)
        {
            try
            {
                _loadingBufferResetEvent.Reset();
                List<PositionModel> buffer = new();
                foreach (OmsPosition position in positions)
                {
                    if (position != null)
                    {
                        PositionModel model = ConvertToUiModel(position);
                        buffer.Add(model);
                    }
                }

                if (buffer.Count > 0)
                {
                    AddMultipleRowsToCollection(buffer);
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddMultipleUpdatedPosition));
            }
            finally
            {
                _loadingBufferResetEvent.Set();
            }
        }

        public void AddUpdatedPosition(OmsPosition position)
        {
            try
            {
                if (position != null)
                {
                    _updatedPositionsQueue.Produce(position);
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddUpdatedPosition));
            }
        }

        private async void PositionUpdateProcessHandler()
        {
            while (true)
            {
                try
                {
                    if (!_loadingBufferResetEvent.IsSet)
                    {
                        await Task.Run(() => _loadingBufferResetEvent.Wait());
                    }
                    var position = (OmsPosition)_updatedPositionsQueue.Consume();
                    PositionModel model = ConvertToUiModel(position);
                    AddRowToCollections(model, true);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(PositionUpdateProcessHandler));
                }
            }
        }

        private PositionModel ConvertToUiModel(OmsPosition position)
        {
            Tuple<string, string> positionId = Tuple.Create(position.AccountAcronym, position.Symbol);
            bool found = _positionIdToPositionModelMap.TryGetValue(positionId, out PositionModel positionModel);
            if (!found)
            {
                positionModel = new PositionModel();
                _positionIdToPositionModelMap[positionId] = positionModel;
            }
            positionModel.Update(position, found);

            switch (MarketDataSubscriptionMode)
            {
                case PortfolioMarketDataSubscriptionMode.Off:
                    positionModel.Unsubscribe();
                    break;
                case PortfolioMarketDataSubscriptionMode.All:
                    positionModel.Subscribe();
                    break;
                case PortfolioMarketDataSubscriptionMode.Open:
                    if (positionModel.NetQty == 0)
                    {
                        positionModel.Unsubscribe();
                    }
                    else
                    {
                        positionModel.Subscribe();
                    }
                    break;
            }

            return positionModel;
        }

        internal bool GetPositionsCollection(string selectedAccount, out ObservableCollection<PositionModel> positionsCollection)
        {
            bool found = _accountToPositionsCollectionMap.TryGetValue(selectedAccount, out positionsCollection);
            return found;
        }

        private void AddMultipleRowsToCollection(List<PositionModel> buffer)
        {
            bool block = buffer.Count > UPDATE_LIMIT;
            try
            {
                if (block)
                {
                    BlockUiEvent?.Invoke();
                }

                foreach (PositionModel position in buffer)
                {
                    AddRowToCollections(position, false);
                }
            }
            finally
            {
                if (block)
                {
                    UnblockUiEvent?.Invoke();
                }
            }
        }

        private void AddRowToCollections(PositionModel position, bool useDispatcher)
        {
            bool addAccount = false;
            if (!_accountToPositionsCollectionMap.TryGetValue(position.Account, out ObservableCollection<PositionModel> positionsCollection))
            {
                positionsCollection = new ObservableCollection<PositionModel>();
                _accountToPositionsCollectionMap[position.Account] = positionsCollection;
                addAccount = true;
            }

            bool isNew = false;
            lock (_positionInsertLock)
            {
                isNew = _addedPositionsKey.Add(position.GetHashCode());
                if (isNew)
                {
                    _addedPositions.Add(position);
                }
            }

            if (isNew)
            {
                AddToCollections();
            }

            void AddToCollections()
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (addAccount)
                    {
                        Accounts.Add(position.Account);
                    }
                    positionsCollection.Add(position);
                    _allAccountsPositionCollection.Add(position);
                }));
            }
        }

        private void OnConnectionStatusChange(bool connected)
        {
            if (!connected)
            {
                ClearTables();
            }
            else if (OpenedPortfolioWindowsCount > 0)
            {
                OmsCore.OrderClient.FirstPortfolioWindowOpened();
            }
        }

        private void ClearTables()
        {
            _positionIdToPositionModelMap.Clear();
            lock (_positionInsertLock)
            {
                _addedPositionsKey.Clear();
                _addedPositions.Clear();
            }

            UpdateSubscriptions(PortfolioMarketDataSubscriptionMode.Off);
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (ObservableCollection<PositionModel> positions in _accountToPositionsCollectionMap.Values)
                {
                    positions.Clear();
                }
                Accounts.Clear();
                _accountToPositionsCollectionMap.Clear();
                _allAccountsPositionCollection.Clear();
                Accounts.Add(_allAccountsKey);
                _accountToPositionsCollectionMap[_allAccountsKey] = _allAccountsPositionCollection;
            }));
        }

        public void UpdateSubscriptions(PortfolioMarketDataSubscriptionMode mode)
        {
            foreach (PositionModel positions in _accountToPositionsCollectionMap.Values.SelectMany(x => x).ToList())
            {
                switch (mode)
                {
                    case PortfolioMarketDataSubscriptionMode.Off:
                        positions.Unsubscribe();
                        break;
                    case PortfolioMarketDataSubscriptionMode.All:
                        positions.Subscribe();
                        break;
                    case PortfolioMarketDataSubscriptionMode.Open:
                        if (positions.NetQty == 0)
                        {
                            positions.Unsubscribe();
                        }
                        else
                        {
                            positions.Subscribe();
                        }
                        break;
                }
            }
        }
    }
}
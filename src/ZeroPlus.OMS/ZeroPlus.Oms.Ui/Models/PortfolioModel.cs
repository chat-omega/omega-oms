using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Ui.Collections;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class PortfolioModel : BindableBase, IPortfolio
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly HashSet<int> _openPositionIds;
        private readonly ConcurrentDictionary<int, IPosition> _idToPositionMap;
        private readonly ConcurrentDictionary<Tuple<string, PositionType>, IPosition> _keyToPortfolioMap;

        private double _AdjustedPnl;
        private double _UnrealizedPnl;

        public int Id { get; set; }
        public int PositionsCount { get; set; }
        public string Name { get; set; }
        public PortfolioType PortfolioType { get; set; }
        public DateTime PortfolioDate { get; set; }

        [Bindable]
        public partial int TotalSubmissions { get; set; }
        [Bindable]
        public partial int GroupSubmissionsAvg { get; set; }
        [Bindable]
        public partial int UniqueSubmissions { get; set; }
        [Bindable]
        public partial int UniqueSpreadSubmissions { get; set; }
        [Bindable]
        public partial int TotalFills { get; set; }
        [Bindable]
        public partial int UniqueFills { get; set; }
        [Bindable]
        public partial int UniqueSpreadFills { get; set; }
        [Bindable]
        public partial int TotalContracts { get; set; }
        [Bindable]
        public partial int StockContracts { get; set; }
        [Bindable]
        public partial int UniqueContracts { get; set; }
        [Bindable]
        public partial int UniqueSpreadContracts { get; set; }
        [Bindable]
        public partial int NetQty { get; set; }
        [Bindable]
        public partial int ShortQty { get; set; }
        [Bindable]
        public partial int LongQty { get; set; }
        [Bindable]
        public partial double FillRate { get; set; }
        [Bindable]
        public partial double OrderFillRate { get; set; }
        [Bindable]
        public partial double IbOrderFillRate { get; set; }
        [Bindable]
        public partial double GroupAvgFillRate { get; set; }
        [Bindable]
        public partial double RealizedPnl { get; set; }
        public double AdjustedPnl
        {
            get => _AdjustedPnl;
            set
            {
                SetValue(ref _AdjustedPnl, value);
                NetPnl = AdjustedPnl + UnrealizedPnl;
            }
        }
        public double UnrealizedPnl
        {
            get => _UnrealizedPnl;
            set
            {
                SetValue(ref _UnrealizedPnl, value);
                NetPnl = AdjustedPnl + UnrealizedPnl;
            }
        }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial int MaxResubmitEstimate { get; set; }
        [Bindable]
        public partial int MaxResubmitForFill { get; set; }
        [Bindable]
        public partial int AvgResubmitEstimate { get; set; }
        [Bindable]
        public partial int AvgResubmitForFill { get; set; }
        [Bindable]
        public partial double DeltaAdjustedBurn { get; set; }
        [Bindable]
        public partial double DeltaAdjustedHelp { get; set; }
        [Bindable]
        public partial double HighestOpenNotional { get; set; }
        [Bindable]
        public partial double TotalOpenNotional { get; set; }
        [Bindable]
        public partial int TotalOutOfMarketOrders { get; set; }
        [Bindable]
        public partial int TotalOutOfMarketFills { get; set; }
        [Bindable]
        public partial int TotalSingleLegSubmissions { get; set; }
        [Bindable]
        public partial int TotalSpreadSubmissions { get; set; }
        [Bindable]
        public partial int TotalSingleFills { get; set; }
        [Bindable]
        public partial int TotalSpreadFills { get; set; }
        [Bindable]
        public partial double LowestRealizedPnl { get; set; }
        [Bindable]
        public partial double HighestRealizedPnl { get; set; }
        [Bindable]
        public partial double LowestAdjustedPnl { get; set; }
        [Bindable]
        public partial double HighestAdjustedPnl { get; set; }
        [Bindable]
        public partial int SubmissionRatePerSec { get; set; }
        [Bindable]
        public partial int MaxOrdersPerSec { get; set; }
        [Bindable]
        public partial int WinnerTrades { get; set; }
        [Bindable]
        public partial int LoserTrades { get; set; }
        [Bindable]
        public partial int SizeWinnerTrades { get; set; }
        [Bindable]
        public partial int SizeLoserTrades { get; set; }
        [Bindable]
        public partial int AvgCloseSubs { get; set; }
        [Bindable]
        public partial double IntroducingBrokerFee { get; set; }
        [Bindable]
        public partial double ExecutingBrokerFee { get; set; }
        [Bindable]
        public partial double ExchangeFee { get; set; }
        [Bindable]
        public partial double OrfFee { get; set; }
        [Bindable]
        public partial double SecFee { get; set; }
        [Bindable]
        public partial double TotalFees { get; set; }
        [Bindable]
        public partial double AvgOpenSubsCount { get; set; }
        [Bindable]
        public partial double AvgSubsBetweenFillsCount { get; set; }
        [Bindable]
        public partial double SingleLegAdjustedPnl { get; set; }
        [Bindable]
        public partial double SpreadAdjustedPnl { get; set; }
        public ICollection<IPosition> Positions => _idToPositionMap.Values;
        public FastObservableCollection<IPosition> AllPositions { get; }
        public FastObservableCollection<IPosition> SymbolPositions { get; }
        public FastObservableCollection<IPosition> UnderlyingPositions { get; }
        public FastObservableCollection<IPosition> SpreadTypePositions { get; }
        public FastObservableCollection<IPosition> RoutePositions { get; }
        public FastObservableCollection<IPosition> ExchangePositions { get; }
        public FastObservableCollection<IPosition> SpreadPositions { get; }
        public FastObservableCollection<IPosition> OpenSpreadPositions { get; }
        public Dispatcher Dispatcher { get; internal set; }

        public PortfolioModel()
        {
            _idToPositionMap = new ConcurrentDictionary<int, IPosition>();
            _keyToPortfolioMap = new ConcurrentDictionary<Tuple<string, PositionType>, IPosition>();
            _openPositionIds = new HashSet<int>();
            Name = string.Empty;
            AllPositions = new FastObservableCollection<IPosition>();
            SymbolPositions = new FastObservableCollection<IPosition>();
            UnderlyingPositions = new FastObservableCollection<IPosition>();
            SpreadTypePositions = new FastObservableCollection<IPosition>();
            RoutePositions = new FastObservableCollection<IPosition>();
            ExchangePositions = new FastObservableCollection<IPosition>();
            SpreadPositions = new FastObservableCollection<IPosition>();
            OpenSpreadPositions = new FastObservableCollection<IPosition>();
        }

        public PortfolioModel(Dispatcher dispatcher) : this()
        {
            Dispatcher = dispatcher;
        }

        public IPosition GetPosition(int positionId, PositionType type)
        {
            try
            {
                if (!_idToPositionMap.TryGetValue(positionId, out IPosition position))
                {
                    position = new PositionSModel()
                    {
                        Id = positionId,
                        PositionType = type,
                    };
                    _idToPositionMap[positionId] = position;

                    switch (position.PositionType)
                    {
                        case PositionType.Underlying:
                        case PositionType.BaseStrategy:
                        case PositionType.Route:
                        case PositionType.Exchange:
                        case PositionType.Spread:
                        case PositionType.Instance:
                        case PositionType.Symbol:
                        case PositionType.Expiration:
                            AddPositionToCollection(position);
                            break;
                    }
                }
                return position;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetPosition));
                return new PositionSModel()
                {
                    Id = positionId,
                    PositionType = type,
                };
            }
        }

        private void AddPositionToCollection(IPosition position)
        {
            try
            {
                if (Dispatcher != null)
                {
                    Dispatcher.BeginInvoke(() => AddToCollection(position));
                }
                else
                {
                    AddToCollection(position);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddPositionToCollection));
            }
        }

        private void AddToCollection(IPosition position)
        {
            switch (position.PositionType)
            {
                case PositionType.Underlying:
                    AllPositions.AddItem(position);
                    UnderlyingPositions.AddItem(position);
                    break;
                case PositionType.BaseStrategy:
                    AllPositions.AddItem(position);
                    SpreadTypePositions.AddItem(position);
                    break;
                case PositionType.Route:
                    AllPositions.AddItem(position);
                    RoutePositions.AddItem(position);
                    break;
                case PositionType.Exchange:
                    AllPositions.AddItem(position);
                    ExchangePositions.AddItem(position);
                    break;
                case PositionType.Spread:
                case PositionType.Instance:
                case PositionType.Symbol:
                case PositionType.Expiration:
                    AllPositions.AddItem(position);
                    break;
            }
        }

        public void AddPosition(IPosition position)
        {
            _keyToPortfolioMap[Tuple.Create(position.Name, position.PositionType)] = position;
        }

        public bool TryGetPosition(string name, PositionType type, out IPosition position)
        {
            Tuple<string, PositionType> key = Tuple.Create(name, type);
            return _keyToPortfolioMap.TryGetValue(key, out position);
        }

        internal void ClearCollections()
        {
            AllPositions.Clear();
            SpreadPositions.Clear();
            SymbolPositions.Clear();
            UnderlyingPositions.Clear();
            SpreadTypePositions.Clear();
            RoutePositions.Clear();
            ExchangePositions.Clear();
            OpenSpreadPositions.Clear();
        }

        internal void ClearData()
        {
            _idToPositionMap.Clear();
            _keyToPortfolioMap.Clear();
            _openPositionIds.Clear();

            Id = 0;
            PortfolioType = 0;
            TotalSubmissions = 0;
            UniqueSubmissions = 0;
            UniqueSpreadSubmissions = 0;
            TotalFills = 0;
            UniqueFills = 0;
            UniqueSpreadFills = 0;
            TotalContracts = 0;
            UniqueContracts = 0;
            UniqueSpreadContracts = 0;
            NetQty = 0;
            ShortQty = 0;
            LongQty = 0;
            FillRate = 0;
            RealizedPnl = 0;
            AdjustedPnl = 0;
            UnrealizedPnl = 0;
            NetDelta = 0;
            MaxResubmitEstimate = 0;
            MaxResubmitForFill = 0;
            AvgResubmitEstimate = 0;
            AvgResubmitForFill = 0;
            AvgCloseSubs = 0;
            TotalSingleLegSubmissions = 0;
            TotalSpreadSubmissions = 0;
            TotalSingleFills = 0;
            TotalSpreadFills = 0;
            LowestRealizedPnl = 0;
            HighestRealizedPnl = 0;
            LowestAdjustedPnl = 0;
            HighestAdjustedPnl = 0;

            SubmissionRatePerSec = 0;
            MaxOrdersPerSec = 0;
        }

        internal void UpdatePosition(IPosition position)
        {
            try
            {
                bool added = _openPositionIds.Contains(position.Id);
                if (position.NetQty == 0 && added)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher.BeginInvoke(() => OpenSpreadPositions.Remove(position));
                    }
                    else
                    {
                        OpenSpreadPositions.Remove(position);
                    }
                    _openPositionIds.Remove(position.Id);
                }
                else if (position.NetQty != 0 && !added)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher.BeginInvoke(() => OpenSpreadPositions.Add(position));
                    }
                    else
                    {
                        OpenSpreadPositions.Add(position);
                    }
                    _openPositionIds.Add(position.Id);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdatePosition));
            }
        }

        public void Clear()
        {
        }
    }
}
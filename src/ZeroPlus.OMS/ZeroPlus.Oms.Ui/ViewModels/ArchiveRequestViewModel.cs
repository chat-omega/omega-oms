using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Services;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ArchiveRequestViewModel : ViewModelBase, IOrderArchiveReceiver
    {
        private static readonly string MODULE_TITLE = "Archive Load";

        private readonly TransactionConsumerModel _transactionConsumerModel;
        private readonly NotificationManager _notificationManager;
        private readonly PortfolioManagerModel _portfolioManagerModel;
        private PortfolioManagerModel _archivePortfolioManagerModel;

        private readonly object _bufferLock = new();
        private readonly Queue<OmsOrderModel> _buffer = new();


        public OmsCore OmsCore { get; }
        public Dispatcher Dispatcher { get; set; }
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        protected IOmsMessageBoxService MessageBoxService => GetService<IOmsMessageBoxService>();

        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial string IsBusyMessage { get; set; }
        [Bindable]
        public partial DateTime StartDateTime { get; set; }
        [Bindable]
        public partial DateTime EndDateTime { get; set; }
        [Bindable]
        public partial bool UseDaysBackForRange { get; set; }
        [Bindable]
        public partial int DaysBack { get; set; }
        [Bindable]
        public partial bool MinFirstEdgeEnabled { get; set; }
        [Bindable]
        public partial double MinFirstEdge { get; set; }
        [Bindable]
        public partial bool MinEdgeToTheoEnabled { get; set; }
        [Bindable]
        public partial double MinEdgeToTheo { get; set; }
        [Bindable]
        public partial bool FillsOnly { get; set; }
        [Bindable]
        public partial bool OpeningOnly { get; set; }
        [Bindable]
        public partial string ApiUsernames { get; set; }
        [Bindable]
        public partial string Tags { get; set; }
        [Bindable]
        public partial string Symbols { get; set; }
        [Bindable]
        public partial string Underlyings { get; set; }

        public FastObservableCollection<OmsOrderModel> ClosedOrdersCollection { get; set; }
        public FastObservableCollection<OmsOrderModel> UniqueOrdersCollection { get; set; }
        public FastObservableCollection<OmsOrderModel> FilledOrdersCollection { get; set; }
        public FastObservableCollection<OmsOrderModel> UniqueFillsCollection { get; set; }
        public IUiUpdateService UiUpdateService { get; internal set; }
        public Action Loaded { get; internal set; }

        public ArchiveRequestViewModel(TransactionConsumerModel transactionConsumerModel, NotificationManager notificationManager, PortfolioManagerModel portfolioManagerModel, OmsCore omsCore)
        {
            _transactionConsumerModel = transactionConsumerModel;
            _notificationManager = notificationManager;
            _portfolioManagerModel = portfolioManagerModel;
            OmsCore = omsCore;
            ModuleTitle = MODULE_TITLE;
            FillsOnly = true;
            DateTime date = DateTime.Today;
            int offset = date.DayOfWeek switch
            {
                DayOfWeek.Monday => 3,
                DayOfWeek.Sunday => 2,
                _ => 1,
            };
            date -= TimeSpan.FromDays(offset);
            StartDateTime = date + TimeSpan.FromHours(8);
            EndDateTime = date + TimeSpan.FromHours(16);
            DaysBack = 1;
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void LoadCommand()
        {
            IsBusy = true;
            List<string> apiUsernames = ParseSeparatedString(ApiUsernames);
            List<string> tags = ParseSeparatedString(Tags);
            List<string> symbols = ParseSeparatedString(Symbols);
            List<string> underlyings = ParseSeparatedString(Underlyings);
            _archivePortfolioManagerModel = new PortfolioManagerModel(_notificationManager)
            {
                OmsCore = OmsCore
            };
            List<OrderStatus> orderStatus = new();
            if (FillsOnly)
            {
                orderStatus.Add(OrderStatus.PartiallyFilled);
                orderStatus.Add(OrderStatus.Filled);
            }

            if (UseDaysBackForRange)
            {
                StartDateTime = DateTime.Today - TimeSpan.FromDays(DaysBack) + TimeSpan.FromHours(8);
                EndDateTime = DateTime.Today + TimeSpan.FromHours(16);
            }

            if (IsArchiveRequestBlockedByProductionPolicy(StartDateTime, EndDateTime, FillsOnly))
            {
                MessageBoxService?.ShowMessage(
                    "The requested date range exceeds the limit allowed during market hours to avoid impacting production.\nPlease contact support to obtain this data, or submit your request outside of market hours (before 7:00 AM or after 3:30 PM, Monday–Friday).",
                    MODULE_TITLE,
                    MessageButton.OK,
                    MessageIcon.Warning);
                IsBusy = false;
                return;
            }

            int portfoliosRequestId = OmsCore.HerculesClient.RequestPnlFromArchive(StartDateTime, EndDateTime, true, true, apiUsernames, tags, symbols, underlyings);
            _portfolioManagerModel.AddRequester(portfoliosRequestId, this);

            int requestId = OmsCore.HerculesClient.RequestTransactionsFromArchive(StartDateTime, EndDateTime, ordersOnly: true, orderStatus, apiUsernames, tags, symbols, underlyings);
            _transactionConsumerModel.AddRequester(requestId, this);
        }

        public void AddMultiplePortfolios(HashSet<IPortfolio> portfolios)
        {
            _archivePortfolioManagerModel.MultiplePortfoliosAdded(0, portfolios);
        }

        public void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            Task.Run(() =>
            {
                List<OmsOrderModel> copy = null;

                lock (_bufferLock)
                {
                    if (lastMessageIndex - orders.Count == 0)
                    {
                        _buffer.Clear();
                    }

                    if (totalQueued == _buffer.Count + orders.Count)
                    {
                        copy = _buffer.ToList();
                        copy.AddRange(orders.Cast<OmsOrderModel>());
                        _buffer.Clear();
                    }
                    else
                    {
                        foreach (var order in orders)
                        {
                            var orderModel = (OmsOrderModel)order;
                            if (orderModel != null)
                            {
                                _buffer.Enqueue(orderModel);
                            }
                        }
                    }
                }

                if (copy != null)
                {
                    foreach (OmsOrderModel order in copy)
                    {
                        order.CheckUnderlying();
                        _archivePortfolioManagerModel.Subscribe(order.SpreadId, SubscriptionFieldType.FirmSpreadPosition, order);
                    }

                    _ = AddMultipleOrdersToCollectionAsync(copy);
                }
            });
        }

        private async Task AddMultipleOrdersToCollectionAsync(List<OmsOrderModel> closedOrders)
        {
            if (MinEdgeToTheoEnabled)
            {
                closedOrders = closedOrders.Where(x => x.EdgeToTheo >= MinEdgeToTheo).ToList();
            }
            if (MinFirstEdgeEnabled)
            {
                closedOrders = closedOrders.Where(x => x.FirstEdgeAcquired && x.FirstEdge >= MinFirstEdge).ToList();
            }
            if (OpeningOnly)
            {
                closedOrders = closedOrders.Where(x => x.PositionEffect != PositionEffect.Close).ToList();
            }

            List<OmsOrderModel> filledOrders = closedOrders.Where(x => x.FilledQty > 0).ToList();
            List<OmsOrderModel> uniqueFills = filledOrders.GroupBy(x => x.SpreadId).Select(g => g.First()).ToList();

            List<OmsOrderModel> uniqueOrders = null;
            if (!FillsOnly)
            {
                uniqueOrders = closedOrders.GroupBy(x => x.SpreadId).Select(g => g.First()).ToList();
            }

            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool blocked = false;
                if (closedOrders.Count > 10_000)
                {
                    UiUpdateService?.BeginUpdate();
                    blocked = true;
                }
                try
                {
                    if (ClosedOrdersCollection != null && closedOrders.Any())
                    {
                        ClosedOrdersCollection.Clear();
                        FilledOrdersCollection.Clear();
                        UniqueOrdersCollection.Clear();
                        UniqueFillsCollection.Clear();

                        FilledOrdersCollection.AddRange(filledOrders);
                        UniqueFillsCollection.AddRange(uniqueFills);

                        if (!FillsOnly)
                        {
                            ClosedOrdersCollection.AddRange(closedOrders);
                            UniqueOrdersCollection.AddRange(uniqueOrders);
                        }
                    }
                    IsBusy = false;
                    Loaded?.Invoke();
                    CurrentWindowService?.Close();
                }
                finally
                {
                    if (blocked)
                    {
                        UiUpdateService?.EndUpdate();
                    }
                }
            }));
        }

        private static bool IsArchiveRequestBlockedByProductionPolicy(DateTime start, DateTime end, bool fillsOnly)
        {
            DateTime now = DateTime.Now.ToEastern();
            bool isWeekday = now.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
            TimeSpan timeOfDay = now.TimeOfDay;
            TimeSpan marketOpen = new TimeSpan(8, 0, 0);
            TimeSpan marketClose = new TimeSpan(16, 30, 0);
            bool isMarketHours = isWeekday && timeOfDay >= marketOpen && timeOfDay <= marketClose;

            double spanDays = (end - start).TotalDays;
            bool rangeTooLarge = fillsOnly ? spanDays > 30 : spanDays > 7;

            return rangeTooLarge && isMarketHours;
        }

        private List<string> ParseSeparatedString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new List<string>();
            }
            else
            {
                return input.Replace(",", ";")
                            .Split(';')
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x.Trim().ToUpper())
                            .ToList();
            }
        }
    }
}

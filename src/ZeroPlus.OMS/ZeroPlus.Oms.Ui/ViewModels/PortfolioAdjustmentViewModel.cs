using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class PortfolioAdjustmentViewModel : CustomizableTableViewModelBase, IOrderArchiveReceiver
    {
        private readonly IHerculesClient _herculesClient;
        private readonly PortfolioManagerModel _portfolioManagerModel;
        private FastObservableCollection<IPortfolio> _portfolios;
        private bool _isLoaded;
        private string _searchTerm;
        private DateTime _targetDate;

        public bool IsLoaded
        {
            get { return _isLoaded; }
            set { SetValue(ref _isLoaded, value); }
        }

        public string SearchTerm
        {
            get { return _searchTerm; }
            set { SetValue(ref _searchTerm, value); }
        }

        public DateTime TargetDate
        {
            get { return _targetDate; }
            set { SetValue(ref _targetDate, value); }
        }

        public FastObservableCollection<IPortfolio> Portfolios
        {
            get { return _portfolios; }
            set { SetValue(ref _portfolios, value); }
        }

        internal Dispatcher Dispatcher { get; private set; }

        public PortfolioAdjustmentViewModel(IHerculesClient herculesClient,
                                            PortfolioManagerModel portfolioManagerModel)
        {
            _herculesClient = herculesClient;
            _portfolioManagerModel = portfolioManagerModel;
            do
            {
                _targetDate = DateTime.Today - TimeSpan.FromDays(1);
            } while (_targetDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            Portfolios = new FastObservableCollection<IPortfolio>();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void LoadCommand()
        {
            List<string> args = new();
            int requestId = _herculesClient.RequestPnlFromArchive(TargetDate.Date, TargetDate.Date, true, true, args, args, args, args);
            _portfolioManagerModel.AddRequester(requestId, this);
        }

        [Command]
        public void ClearCommand()
        {
            Dispatcher.BeginInvoke(() =>
            {
                Portfolios.Clear();
                IsLoaded = false;
            });
        }

        [Command]
        public void UpdateCommand()
        {
            if (Portfolios.Count > 0)
            {
                MessageResult result = MessageBoxService.ShowMessage($"Are you sure you want to update?", "Portfolio Adjustment", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
                if (result == MessageResult.Yes)
                {
                    _herculesClient.RequestPortfolioUpdate(Portfolios.ToList());
                }
            }
        }

        public void AddMultiplePortfolios(HashSet<IPortfolio> portfolios)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Portfolios.Clear();
                Portfolios.AddRange(portfolios.ToList());
                IsLoaded = true;
            });
        }

        public void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex) { /*Ignore*/ }
    }
}

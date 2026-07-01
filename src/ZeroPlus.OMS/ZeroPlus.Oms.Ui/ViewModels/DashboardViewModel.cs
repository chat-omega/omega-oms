using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Charts;
using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DashboardViewModel : ModuleViewModelBase, IChartModule
    {
        public override Module Module { get; protected set; } = Module.Dashboard;


        public PortfolioManagerModel PortfolioManager { get; }

        [Bindable]
        public partial PortfolioModel SelectedPortfolio { get; set; }

        [Bindable]
        public partial FastObservableCollection<IPosition> UnderlyingPositions { get; set; }

        [Bindable]
        public partial FastObservableCollection<IPosition> SpreadTypePositions { get; set; }

        [Bindable]
        public partial FastObservableCollection<IPosition> RoutePositions { get; set; }

        [Bindable]
        public partial FastObservableCollection<IPosition> ExchangePositions { get; set; }

        [Bindable]
        public partial FastObservableCollection<LatencyChartSeriesModel> ChartSeries { get; set; }

        public SeriesAggregateFunction AggregateFunction { get; set; } = SeriesAggregateFunction.Average;

        public DashboardViewModel(PortfolioManagerModel portfolioManager, ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            ChartSeries = new FastObservableCollection<LatencyChartSeriesModel>();
            PortfolioManager = portfolioManager;
            SelectedPortfolio = PortfolioManager.FirmPortfolios.FirstOrDefault();
            UnderlyingPositions = SelectedPortfolio?.UnderlyingPositions;
            SpreadTypePositions = SelectedPortfolio?.SpreadTypePositions;
            RoutePositions = SelectedPortfolio?.RoutePositions;
            ExchangePositions = SelectedPortfolio?.ExchangePositions;
        }

        [Command]
        public void DashboardAlertsCommand()
        {
            try
            {
                AlertConfigurationView view = new();
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DashboardAlertsCommand));
            }
        }

        [Command]
        public void SelectedPortfolioChangedCommand()
        {
            try
            {
                UnderlyingPositions = SelectedPortfolio?.UnderlyingPositions;
                SpreadTypePositions = SelectedPortfolio?.SpreadTypePositions;
                RoutePositions = SelectedPortfolio?.RoutePositions;
                ExchangePositions = SelectedPortfolio?.ExchangePositions;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SelectedPortfolioChangedCommand));
            }
        }

        [Command]
        public void BreakdownPortfolioByUnderlyingCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Underlying);
        }

        [Command]
        public void BreakdownPortfolioByStrategyCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.BaseStrategy);
        }

        [Command]
        public void BreakdownPortfolioBySpreadCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Spread);
        }

        [Command]
        public void BreakdownPortfolioBySymbolCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Symbol);
        }

        [Command]
        public void BreakdownPortfolioByInstanceCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Instance);
        }

        [Command]
        public void BreakdownPortfolioByRouteCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Route);
        }

        [Command]
        public void BreakdownPortfolioByExchangeCommand(PortfolioModel portfolioModel)
        {
            ShowBreakdownView(portfolioModel, PositionType.Exchange);
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return PortfolioManager.SerializeAlertsToJson();
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            await PortfolioManager.LoadAlertsFromJsonAsync(configJson);
        }

        private void ShowBreakdownView(PortfolioModel portfolioModel, PositionType positionType)
        {
            try
            {
                PositionsView positionsView = new();
                if (positionsView.DataContext is PositionsViewModel dataContext)
                {
                    dataContext.LoadPositions(portfolioModel, positionType);
                }
                positionsView.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BreakdownPortfolioByUnderlyingCommand));
            }
        }
    }
}

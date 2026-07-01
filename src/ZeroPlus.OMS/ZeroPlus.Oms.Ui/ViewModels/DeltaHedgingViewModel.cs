using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum LimitHandling
    {
        HitBest,
        Lean,
        MidPoint,
        Last,
    }

    public enum GammaScalpTriggerMode
    {
        Mid,
        Ema,
        Delta,
    }

    public partial class DeltaHedgingViewModel : CustomizableTableViewModelBase
    {
        private static readonly string MODULE_TITLE = "Delta Hedging";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();

        public static List<OrderType> OrderTypes { get; } = ((OrderType[])Enum.GetValues(typeof(OrderType))).ToList();
        public static List<LimitHandling> LimitHandlingOptions { get; } = ((LimitHandling[])Enum.GetValues(typeof(LimitHandling))).ToList();

        public OmsCore OmsCore { get; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        public PortfolioManagerModel PortfolioManagerModel { get; }

        public DeltaHedgingViewModel(PortfolioManagerModel portfolioManagerModel, OmsCore omsCore)
        {
            OmsCore = omsCore;
            ModuleTitle = MODULE_TITLE;
            PortfolioManagerModel = portfolioManagerModel;
            PortfolioManagerModel.LoadHedgeRoutes();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void AddColumn()
        {
            AddColumnView addColumnView = new();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent += OnAddColumnEvent;
            addColumnView.ShowDialog();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent -= OnAddColumnEvent;
        }

        private void OnAddColumnEvent(CustomColumnTemplateModel colTemplate)
        {
            LoadCustomColumnService.AddCustomColumn(colTemplate);
        }

        [Command]
        public void AddCommand()
        {
            AddHedgeUnderlyingView managementView = new();
            if (managementView.DataContext is AddHedgeUnderlyingViewModel positionViewModel)
            {
                managementView.ShowDialog();
                if (!string.IsNullOrWhiteSpace(positionViewModel.Symbol))
                {
                    PortfolioManagerModel.AddSymbol(positionViewModel.Symbol);
                }
            }
            else
            {
                _log.Error(nameof(AddCommand) + " add position manager load failed.");
            }
        }

        [Command]
        public void OpenInPositionManagerCommand(UnderlyingPositionModel hedgePositionModel)
        {
            HedgePositionManagementView managementView = new();
            if (managementView.DataContext is HedgePositionManagementViewModel positionManagementViewModel)
            {
                positionManagementViewModel.UnderlyingPositionModel = hedgePositionModel;
                managementView.Show();
            }
            else
            {
                _log.Error(nameof(OpenInPositionManagerCommand) + " position manager load failed.");
            }
        }

        [Command]
        public void ActivateAllCommand()
        {
            try
            {
                for (int i = 0; i < PortfolioManagerModel.UserPositions.Count; i++)
                {
                    UnderlyingPositionModel position = PortfolioManagerModel.UserPositions[i];
                    position.Active = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void DeactivateAllCommand()
        {
            try
            {
                for (int i = 0; i < PortfolioManagerModel.UserPositions.Count; i++)
                {
                    UnderlyingPositionModel position = PortfolioManagerModel.UserPositions[i];
                    position.Active = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void StartStopCommand()
        {
            PortfolioManagerModel.IsHedging = !PortfolioManagerModel.IsHedging;
        }

        [Command]
        public void Clone()
        {
            try
            {
                DeltaHedgingView view = new();
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.DeltaHedgingLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        internal void Dispose()
        {
        }
    }
}

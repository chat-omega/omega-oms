using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LockTraderViewModel : BasketTraderViewModel
    {
        private bool _settingsLocked;


        public override Module Module { get; protected set; } = Module.LockTrader;

        [Bindable]
        public partial bool PriceUnlocked { get; set; }
        [Bindable]
        public partial double LockTraderPriceMin { get; set; }
        [Bindable]
        public partial double LockTraderPriceMax { get; set; }

        public ModifyStagedOrdersViewModel LockTraderModifyPriceQtyModel { get; set; } = new()
        {
            Price = -0.05,
            Qty = 1,
        };

        public bool LockSettingsLocked
        {
            get => _settingsLocked;
            set
            {
                if (value || Verified())
                {
                    SetValue(ref _settingsLocked, value);
                }
            }
        }

        public LockTraderViewModel(ILogger<BasketTraderViewModel> logger,
                                   IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
                                   IAbstractFactory<RouteSelectionViewModel> routeSelectionViewFactory,
                                   IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
                                   IAbstractFactory<CustomEdgeFunctionEditorView> customEdgeFunctionEditorView,
                                   VolTradersManager volTradersManager,
                                   TransactionConsumerModel transactionConsumer,
                                   PortfolioManagerModel portfolioManagerModel,
                                   NotificationManager notificationManager,
                                   DominatorsManagerModel dominatorsManagerModel,
                                   ConfigBrowserViewModel configBrowserViewModel,
                                   BasketGroupManagerModel basketGroupManagerModel,
                                   OmsCore omsCore,
                                   IModuleFactory moduleFactory)
            : base(logger,
                   ticketFactory,
                   routeSelectionViewFactory,
                   threeWayCloserFactory,
                   customEdgeFunctionEditorView,
                   volTradersManager,
                   transactionConsumer,
                   portfolioManagerModel,
                   notificationManager,
                   dominatorsManagerModel,
                   configBrowserViewModel,
                   basketGroupManagerModel,
                   omsCore,
                   moduleFactory)
        {
            ModuleTitle = MODULE_TITLE;
            BasketType = Enums.BasketType.LockTrader;
            LockSettingsLocked = true;
            LockTraderModifyPriceQtyModel.ModifyBasketQtyPxEvent += ModifyAllPxQty;
            SetupLockTrader();
            UpdatePriceLimitsCommand();
        }

        [Command]
        public void UpdatePriceLimitsCommand()
        {
            if (BasketItems.Any())
            {
                LockTraderPriceMin = double.NaN;
                LockTraderPriceMax = double.NaN;
                foreach (var basketItem in BasketItems)
                {
                    if (OmsCore.Config.LockTraderPriceLimits.TryGetValue(basketItem.BaseStrategy, out var limit))
                    {
                        CheckForMinLimit(limit.MinPrice);
                        CheckForMaxLimit(limit.MaxPrice);
                    }
                    else
                    {
                        CheckForMinLimit(OmsCore.Config.LockTraderPriceMin);
                        CheckForMaxLimit(OmsCore.Config.LockTraderPriceMax);
                    }
                }
            }
            else
            {
                LockTraderPriceMin = OmsCore.Config.LockTraderPriceMin;
                LockTraderPriceMax = OmsCore.Config.LockTraderPriceMax;
            }

            if (LockTraderModifyPriceQtyModel.Price > LockTraderPriceMax)
            {
                LockTraderModifyPriceQtyModel.Price = LockTraderPriceMax;
            }
        }

        private void CheckForMinLimit(double min)
        {
            if (LockTraderPriceMin < min || double.IsNaN(LockTraderPriceMin))
            {
                LockTraderPriceMin = min;
            }
        }

        private void CheckForMaxLimit(double max)
        {
            if (LockTraderPriceMax > max || double.IsNaN(LockTraderPriceMax))
            {
                LockTraderPriceMax = max;
            }
        }

        [Command]
        public void LockSettings()
        {
            if (!LockSettingsLocked)
            {
                LockSettingsLocked = true;
            }

            if (LockTraderModifyPriceQtyModel.UpdatePrice)
            {
                LockTraderModifyPriceQtyModel.UpdatePrice = false;
            }

            if (LockTraderModifyPriceQtyModel.UpdateQty)
            {
                LockTraderModifyPriceQtyModel.UpdateQty = false;
            }
        }

        private bool Verified()
        {
            bool ok = MessageBoxService?.Show("Are you sure you want to unlock settings.",
                "Verification",
                MessageButton.YesNo,
                MessageIcon.Exclamation,
                MessageResult.No) == MessageResult.Yes;
            return ok;
        }

        protected override bool CanCancelQueuedOrders()
        {
            return SubmitAllRunning;
        }

        protected override void OnConfigLoaded()
        {
            SetupLockTrader();
        }

        [Command]
        public override Task SetEdgeAsync(BasketTraderItemModel basketItem)
        {
            basketItem.SetPrice(LockTraderModifyPriceQtyModel.Price);
            return Task.CompletedTask;
        }

        internal void SetupLockTrader()
        {
            LockSettingsLocked = true;

            BasketSettings.AdjustPriceBeforeSubmit = false;

            BasketSettings.UseEdgeToTheo = false;
            BasketSettings.UseEdgeToHistoricBest = false;
            BasketSettings.UseEdgeToAdjTheo = false;
            BasketSettings.UseLastFillAdjPx = false;
            BasketSettings.UseCustomFunctionEdge = false;
            BasketSettings.UseEdgeToMid = false;
            BasketSettings.UseEdgeToEma = false;
            BasketSettings.UseEdgeToTheoAndMid = false;
            BasketSettings.UseEdgeToTheoStopMid = false;
            BasketSettings.UseEdgeToEmaStopMid = false;
            BasketSettings.UseEdgeToMidStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = false;
            BasketSettings.UseBidPercent = false;
            BasketSettings.UseEdgeToEmaBid = false;
            BasketSettings.UseEdgeToBid = false;
            BasketSettings.UsePermAdjPx = false;
            BasketSettings.UseBestOfEdge = false;

            BasketSettings.FishModeEnabled = false;

            BasketSettings.DeltaCapEnabled = false;
            BasketSettings.StrikeCapEnabled = false;
            BasketSettings.WidthCapEnabled = false;
            BasketSettings.DynamicUpdateEdgeOverrides = false;
            BasketSettings.EvaluateAdjustedEdgeOverrides = false;
            BasketSettings.ModifyPxWithMktChange = false;

            BasketSettings.QueueCancel = true;
            BasketSettings.ResubmitAfterCancel = false;
            BasketSettings.CancelOnClose = false;
            BasketSettings.DerivedValuesEnabled = false;
            BasketSettings.RiskCheckEnabled = false;
            BasketSettings.CancelWithEdgeToTheoEnabled = false;
            BasketSettings.CancelWithEdgeToAdjTheoEnabled = false;
            BasketSettings.CancelWithEdgeToMidEnabled = false;
            BasketSettings.CancelWithWidthEnabled = false;
            BasketSettings.CancelWithUnderlyingPxEnabled = false;
            BasketSettings.CancelWithUnderlyingDeltaPxEnabled = false;
            BasketSettings.CancelWithTimerEnabled = false;
            BasketSettings.CancelWithTimer = 0;

            BasketSettings.SubmitWithDelayEnabled = true;

            var automationConfigs = GetAutomationConfigs();
            foreach (var automationConfig in automationConfigs)
            {
                automationConfig.CloseOrderMode = null;
                automationConfig.GoFishAutoCloseEnabled = automationConfig.LockTraderAutoCloseEnabled;
                automationConfig.LoopingEnabled = false;
                automationConfig.LoopFreeLook = false;
                automationConfig.FreeLookWhenGettingCloseEdge = false;
                automationConfig.LoopFreeLookOnAll = false;
                automationConfig.AutoHedgeOnClose = false;
                automationConfig.AutoHedgeOnCloseSizeOnly = false;

                automationConfig.LoopFreeLookOnNickelNames = false;
                automationConfig.LoopFreeLookOnDimeNames = false;
            }
        }
    }
}

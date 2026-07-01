using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using NLog;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LoopAdvancedConfigsViewModel : ViewModelBase
    {
        protected static readonly ILogger _log = LogManager.GetCurrentClassLogger();


        public AutoAggressorMode[] AutoAggressorModes { get; } = (AutoAggressorMode[])Enum.GetValues(typeof(AutoAggressorMode));
        public AutoAggressorEdgeTightenMode[] AutoAggressorEdgeTightenModes { get; } = (AutoAggressorEdgeTightenMode[])Enum.GetValues(typeof(AutoAggressorEdgeTightenMode));
        public LoopPricingMode[] LoopPricingModes { get; } = (LoopPricingMode[])Enum.GetValues(typeof(LoopPricingMode));
        public LoopIntervalType[] LoopIntervalTypes { get; } = (LoopIntervalType[])Enum.GetValues(typeof(LoopIntervalType));

        [Bindable]
        public partial BasketTraderViewModel BasketTrader { get; set; }
        [Bindable]
        public partial BasketSettings BasketSettings { get; set; }

        [Command]
        public void ClearAutomationConfigCommand()
        {
            BasketTrader.ClearAutomationConfigCommand();
        }

        [Command]
        public void ConfigAutomationConfigCommand()
        {
            BasketTrader.ConfigAutomationConfigCommand();
        }

        [Command]
        public void ShowDynamicIntervalConfigPanelCommand()
        {
            BasketTrader.ShowDynamicIntervalConfigPanelCommand();
        }

        [Command]
        public void SaveAutomationConfigCommand()
        {
            BasketTrader.SaveAutomationConfigCommand();
        }

        [Command]
        public void ShowExchangeToRouteMappingEditorCommand()
        {
            try
            {
                ExchToRouteMapConfigView view = new();
                var automationConfig = BasketTrader.GetAutomationConfig();
                if (view.DataContext is ExchToRouteMapConfigViewModel viewModel)
                {
                    if (automationConfig.ExchToRouteMap != null)
                    {
                        foreach (var kvp in automationConfig.ExchToRouteMap)
                        {
                            viewModel.Mapping.Add(new ExchToRouteMapModel { Exchange = kvp.Key?.ToUpper(), Route = kvp.Value?.ToUpper() });
                        }
                    }

                    viewModel.MappingUpdated += map => automationConfig.ExchToRouteMap = map;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowExchangeToRouteMappingEditorCommand));
            }
        }

        [Command]
        public void DeleteConfigCommand(AutomationConfigModel automationConfigModel)
        {
            BasketTrader.DeleteConfigCommand(automationConfigModel);
        }
    }
}

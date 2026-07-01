using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class HedgeHouseViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        public PortfolioManagerModel PortfolioManagerModel { get; }

        public HedgeHouseViewModel(PortfolioManagerModel portfolioManagerModel)
        {
            ModuleTitle = "Hedge House";
            PortfolioManagerModel = portfolioManagerModel;
        }

        [Command]
        public void DeltaNeutralChangedCommand(SymbolHedgeManagerModel model)
        {
            try
            {
                if (model.DeltaNeutralEnabled)
                {
                    if (model.Ticket.TotalGamma < 0)
                    {
                        MessageResult result = MessageBoxService.ShowMessage($"Delta Neutral mode enabled for a {(model.Ticket.IsSingleLeg ? "symbol" : "spread")} with negative gamma.\nAre you sure you want to keep it on?", "Hedge House", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
                        if (result == MessageResult.No)
                        {
                            model.DeltaNeutralEnabled = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeltaNeutralChangedCommand));
            }
        }

        [Command]
        public void HedgeHouseEnabledChangedCommand()
        {
            try
            {
                PortfolioManagerModel.ResetHedgeHouseState();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartCommand));
            }
        }

        [Command]
        public void HedgeCommand(SymbolHedgeManagerModel model)
        {
            try
            {
                int req = model.HedgeReqQty;
                if (req != 0)
                {
                    MessageResult result = MessageBoxService.ShowMessage($"Are you sure you want to hedge {req} {model.Underlying} for \n{model.Description}?", "Hedge House", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
                    if (result == MessageResult.Yes)
                    {
                        model.SubmitHedge(req);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HedgeCommand));
            }
        }

        [Command]
        public void StartCommand(SymbolHedgeManagerModel model)
        {
            try
            {
                model.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartCommand));
            }
        }

        [Command]
        public void StopCommand(SymbolHedgeManagerModel model)
        {
            try
            {
                model.Stop();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopCommand));
            }
        }

        internal void Dispose()
        {
        }
    }
}

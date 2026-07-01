using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models.OrderRouting;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Excel;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;
using Venue = ZeroPlus.Models.Data.Enums.Venue;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DominatorsManagerViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private DelegateCommand<object> _checkAllWithSameValueCommand;

        public DominatorsManagerModel DominatorsManager { get; }
        public static EdgeType[] EdgeTypes { get; } = Enum.GetValues<EdgeType>();
        public static Venue[] Venues { get; } = Enum.GetValues<Venue>();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public string Uid { get; internal set; }
        public Dispatcher Dispatcher { get; set; }

        protected IUIObjectService AutomationComboBoxService => GetService<IUIObjectService>("AutomationComboBoxService");
        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();

        [Bindable]
        public partial bool Listening { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Message { get; set; }

        public DominatorsManagerViewModel(DominatorsManagerModel dominatorsManagerModel)
        {
            DominatorsManager = dominatorsManagerModel;
            ModuleTitle = "Doms Manager";
            OmsCore.DominatorsManager.ServerStatusChangedEvent += DominatorsManager_ServerStatusChangedEvent;
            OmsCore.DominatorsManager.DominatorMessageEvent += DominatorsManager_DominatorMessageEvent;
            DominatorsManager_ServerStatusChangedEvent(OmsCore.DominatorsManager.Listening);

            if (!OmsCore.DominatorsManager.Listening && OmsCore.Config.DominatorsManagerListenerEnabled)
            {
                _ = OmsCore.DominatorsManager.StartServerAsync();
            }
#if DEBUG
            dominatorsManagerModel.ZZZ_ADD_Dominator();
#endif
        }

        [Command]
        public void RestartServerCommand()
        {
            try
            {
                _ = OmsCore.DominatorsManager.StopServerAsync().ContinueWith(t => OmsCore.DominatorsManager.StartServerAsync());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestartServerCommand));
            }
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public ICommand CheckAllWithSameValueCommand
        {
            get
            {
                _checkAllWithSameValueCommand ??= new DelegateCommand<object>(CheckAllWithSameValue);

                return _checkAllWithSameValueCommand;
            }
        }

        private void CheckAllWithSameValue(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    UncheckAll();
                    IEnumerable<DominatorModel> domsToActivate = null;
                    switch ((string)parameter.Field.ToString())
                    {
                        case nameof(DominatorModel.Active):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Active == parameter.Value);
                            break;
                        case nameof(DominatorModel.Source):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Source == parameter.Value);
                            break;
                        case nameof(DominatorModel.Host):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Host == parameter.Value);
                            break;
                        case nameof(DominatorModel.Trader):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Trader == parameter.Value);
                            break;
                        case nameof(DominatorModel.Setup):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Setup == parameter.Value);
                            break;
                        case nameof(DominatorModel.Configs):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Configs == parameter.Value);
                            break;
                        case nameof(DominatorModel.RealizedPnl):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.RealizedPnl == parameter.Value);
                            break;
                        case nameof(DominatorModel.AdjustedPnl):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.AdjustedPnl == parameter.Value);
                            break;
                        case nameof(DominatorModel.State):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.State == parameter.Value);
                            break;
                        case nameof(DominatorModel.IsRunning):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.IsRunning == parameter.Value);
                            break;
                        case nameof(DominatorModel.DomCount):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.DomCount == parameter.Value);
                            break;
                        case nameof(DominatorModel.Fills):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Fills == parameter.Value);
                            break;
                        case nameof(DominatorModel.DeltaMax):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.DeltaMax == parameter.Value);
                            break;
                        case nameof(DominatorModel.EdgeMultiplier):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.EdgeMultiplier == parameter.Value);
                            break;
                        case nameof(DominatorModel.Type):
                            domsToActivate = DominatorsManager.Dominators.ToList().Where(x => x.Type == parameter.Value);
                            break;
                    }

                    if (domsToActivate != null)
                    {
                        foreach (DominatorModel item in domsToActivate)
                        {
                            item.Active = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckAllWithSameValue));
            }
        }

        [Command]
        public void CloneCommand()
        {
            try
            {
                DominatorsManagerView view = new();
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloneCommand));
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
                    viewModel.SetModule(Module.DominatorsManagerLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void SaveSettings()
        {
            try
            {
                DominatorsManager.SaveDominatorSettings();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveSettings));
            }
        }

        [Command]
        public void CheckAll()
        {
            try
            {
                foreach (DominatorModel item in DominatorsManager.Dominators)
                {
                    item.Active = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckAll));
            }
        }

        [Command]
        public void UncheckAll()
        {
            try
            {
                foreach (DominatorModel item in DominatorsManager.Dominators)
                {
                    item.Active = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UncheckAll));
            }
        }

        [Command]
        public void ModifySelected()
        {
            try
            {
                List<Dictionary<int, double>> calendarEdge = DominatorsManager.Dominators.Where(x => x.Active).Select(x => x.Dominator.CalendarEdge).ToList();
                List<Dictionary<double, double>> deltaEdge = DominatorsManager.Dominators.Where(x => x.Active).Select(x => x.Dominator.DeltaEdge).ToList();
                ModifyStagedDominatorsViewModel modifyStagedOrdersViewModel = new(calendarEdge, deltaEdge);

                modifyStagedOrdersViewModel.ModifyDomsEvent += (bool updateEdgeEnabled,
                                                                double edgeMultiplier,
                                                                bool updateDeltaMax,
                                                                double deltaMax,
                                                                bool updateLoopSize,
                                                                int loopSize,
                                                                bool updateDaysToExpiration,
                                                                int minDaysToExpiration,
                                                                int maxDaysToExpiration,
                                                                List<Tuple<int, double>> timeToEdgeSettings,
                                                                List<Tuple<double, double>> deltaToEdgeSettings) =>
                ModifyAll(updateEdgeEnabled,
                          edgeMultiplier,
                          updateDeltaMax,
                          deltaMax,
                          updateLoopSize,
                          loopSize,
                          updateDaysToExpiration,
                          minDaysToExpiration,
                          maxDaysToExpiration,
                          timeToEdgeSettings,
                          deltaToEdgeSettings,
                          DominatorsManager.Dominators.Where(x => x.Active).ToList());

                ModifyStagedDomsView view = new()
                {
                    DataContext = modifyStagedOrdersViewModel
                };

                view.Closed += (object sender, EventArgs e) =>
                modifyStagedOrdersViewModel.ModifyDomsEvent -= (bool updateEdgeEnabled,
                                                                double edgeMultiplier,
                                                                bool updateDeltaMax,
                                                                double deltaMax,
                                                                bool updateLoopSize,
                                                                int loopSize,
                                                                bool updateDaysToExpiration,
                                                                int minDaysToExpiration,
                                                                int maxDaysToExpiration,
                                                                List<Tuple<int, double>> timeToEdgeSettings,
                                                                List<Tuple<double, double>> deltaToEdgeSettings) =>
                ModifyAll(updateEdgeEnabled,
                          edgeMultiplier,
                          updateDeltaMax,
                          deltaMax,
                          updateLoopSize,
                          loopSize,
                          updateDaysToExpiration,
                          minDaysToExpiration,
                          maxDaysToExpiration,
                          timeToEdgeSettings,
                          deltaToEdgeSettings,
                          DominatorsManager.Dominators.Where(x => x.Active).ToList());

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifySelected));
            }
        }

        [Command]
        public void ModifyAll()
        {
            try
            {
                List<Dictionary<int, double>> calendarEdge = DominatorsManager.Dominators.Where(x => x.Active).Select(x => x.Dominator.CalendarEdge).ToList();
                List<Dictionary<double, double>> deltaEdge = DominatorsManager.Dominators.Where(x => x.Active).Select(x => x.Dominator.DeltaEdge).ToList();
                ModifyStagedDominatorsViewModel modifyStagedOrdersViewModel = new(calendarEdge, deltaEdge);

                modifyStagedOrdersViewModel.ModifyDomsEvent += (bool updateEdgeEnabled,
                                                                double edgeMultiplier,
                                                                bool updateDeltaMax,
                                                                double deltaMax,
                                                                bool updateLoopSize,
                                                                int loopSize,
                                                                bool updateDaysToExpiration,
                                                                int minDaysToExpiration,
                                                                int maxDaysToExpiration,
                                                                List<Tuple<int, double>> timeToEdgeSettings,
                                                                List<Tuple<double, double>> deltaToEdgeSettings) =>
                ModifyAll(updateEdgeEnabled,
                          edgeMultiplier,
                          updateDeltaMax,
                          deltaMax,
                          updateLoopSize,
                          loopSize,
                          updateDaysToExpiration,
                          minDaysToExpiration,
                          maxDaysToExpiration,
                          timeToEdgeSettings,
                          deltaToEdgeSettings,
                          DominatorsManager.Dominators.ToList());

                ModifyStagedDomsView view = new()
                {
                    DataContext = modifyStagedOrdersViewModel
                };

                view.Closed += (object sender, EventArgs e) =>
                modifyStagedOrdersViewModel.ModifyDomsEvent -= (bool updateEdgeEnabled,
                                                                double edgeMultiplier,
                                                                bool updateDeltaMax,
                                                                double deltaMax,
                                                                bool updateLoopSize,
                                                                int loopSize,
                                                                bool updateDaysToExpiration,
                                                                int minDaysToExpiration,
                                                                int maxDaysToExpiration,
                                                                List<Tuple<int, double>> timeToEdgeSettings,
                                                                List<Tuple<double, double>> deltaToEdgeSettings) =>
                ModifyAll(updateEdgeEnabled,
                          edgeMultiplier,
                          updateDeltaMax,
                          deltaMax,
                          updateLoopSize,
                          loopSize,
                          updateDaysToExpiration,
                          minDaysToExpiration,
                          maxDaysToExpiration,
                          timeToEdgeSettings,
                          deltaToEdgeSettings,
                          DominatorsManager.Dominators.ToList());

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifySelected));
            }
        }

        [Command]
        public async void StartStaged()
        {
            try
            {
                bool ok = false;
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ok = MessageBoxService?.Show(string.Format("Are you sure you want to start all staged dominators?"),
                                                  "Confirm",
                                                  MessageButton.YesNo,
                                                  MessageIcon.Exclamation,
                                                  MessageResult.No) == MessageResult.Yes;
                }));
                if (ok)
                {
                    Parallel.ForEach(DominatorsManager.Dominators.Where(x => x.Staged).ToList(), dominator => dominator.StartAsync());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartStaged));
            }
        }

        [Command]
        public async void StartSelected()
        {
            try
            {
                bool ok = false;
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ok = MessageBoxService?.Show(string.Format("Are you sure you want to start all active dominators?"),
                                                  "Confirm",
                                                  MessageButton.YesNo,
                                                  MessageIcon.Exclamation,
                                                  MessageResult.No) == MessageResult.Yes;
                }));
                if (ok)
                {
                    Parallel.ForEach(DominatorsManager.Dominators.Where(x => x.Active).ToList(), dominator => dominator.StartAsync());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartSelected));
            }
        }

        [Command]
        public async void StartAll()
        {
            try
            {
                bool ok = false;
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ok = MessageBoxService?.Show(string.Format("Are you sure you want to start all dominators?"),
                                                  "Confirm",
                                                  MessageButton.YesNo,
                                                  MessageIcon.Exclamation,
                                                  MessageResult.No) == MessageResult.Yes;
                }));
                if (ok)
                {
                    Parallel.ForEach(DominatorsManager.Dominators.ToList(), dominator => dominator.StartAsync());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartAll));
            }
        }

        [Command]
        public void StopStaged()
        {
            try
            {
                Parallel.ForEach(DominatorsManager.Dominators.Where(x => x.Staged).ToList(), dominator => dominator.StopAsync());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopStaged));
            }
        }

        [Command]
        public void StopSelected()
        {
            try
            {
                Parallel.ForEach(DominatorsManager.Dominators.Where(x => x.Active).ToList(), dominator => dominator.StopAsync());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopSelected));
            }
        }

        [Command]
        public void StopAll()
        {
            try
            {
                Parallel.ForEach(DominatorsManager.Dominators.ToList(), dominator => dominator.StopAsync());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAll));
            }
        }

        [Command]
        public void AllowUniqueSpreadsChangedCommand()
        {
            try
            {
                Parallel.ForEach(DominatorsManager.Dominators.ToList(), dominator =>
                {
                    if (DominatorsManager.AllowUniqueSpreads)
                    {
                        dominator.AllowUniqueSubmissionsAsync();
                    }
                    else
                    {
                        dominator.BlockUniqueSubmissionsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AllowUniqueSpreadsChangedCommand));
            }
        }

        [Command]
        public void LoadSetup()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selected = DominatorsManager.Dominators.Where(x => x.Active).ToList();

            LoadSetupViewModel viewModel = new(selected);

            LoadSetupView view = new()
            {
                DataContext = viewModel
            };

            view.ShowDialog();
        }

        [Command]
        public void LoadList()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }


            List<DominatorModel> selected = DominatorsManager.Dominators.Where(x => x.Active).ToList();

            LoadDomListViewModel viewModel = new(selected);

            LoadDomListView view = new()
            {
                DataContext = viewModel
            };

            view.ShowDialog();
        }

        [Command]
        public void RemoveHighDeltaSpreadsAndStartCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.RemoveHighDeltaSpreadsAndStart();
            }
        }

        [Command]
        public void LoadEmaCaptureCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.LoadEmaCapture();
            }
        }

        [Command]
        public void DisplayFirmTradeActivityCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.DisplayFirmTradeActivity();
            }
        }

        [Command]
        public void EnableLoadPriceChainCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.ChangeLoadPriceChain(enable: true);
            }
        }

        [Command]
        public void DisableLoadPriceChainCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.ChangeLoadPriceChain(enable: false);
            }
        }

        [Command]
        public void EnableLeastDataOptionCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.ChangeLeastDataPossible(enable: true);
            }
        }

        [Command]
        public void DisableLeastDataOptionCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.ChangeLeastDataPossible(enable: false);
            }
        }

        [Command]
        public void SelectChannelCommand(string channel)
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }
            switch (channel)
            {
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9":
                    break;
                default:
                    return;
            }
            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.SelectChannel(channel: channel);
            }
        }

        [Command]
        public void ChangeRouteCommand(string route)
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }

            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.ChangeRoute(route: route);
            }
        }

        [Command]
        public void ShowStatusCommand(DominatorModel dominatorModel)
        {
            System.Collections.Concurrent.ConcurrentDictionary<string, string> statusMap = dominatorModel.Dominator.StatusUpdateMap;
            if (statusMap.IsEmpty)
            {
                MessageBoxService?.ShowMessage("No status update found.", dominatorModel.Instance);
            }
            else
            {
                MessageBoxService?.ShowMessage(string.Join("\n", statusMap.Select(x => x.Key + ":" + x.Value)), dominatorModel.Instance);
            }
        }

        [Command]
        public void SaveLogsCommand()
        {
            if (!DominatorsManager.Dominators.Any(x => x.Active))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage("No workbook selected.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Error);
                }));
                return;
            }


            List<DominatorModel> selectedDominators = DominatorsManager.Dominators.Where(x => x.Active).ToList();
            int logDelay = 30;
            for (int i = 0; i < selectedDominators.Count; i++)
            {
                DominatorModel dominator = selectedDominators[i];
                dominator.SaveLog(delay: (i + 1) * logDelay);
            }
        }

        [Command]
        public void StartExcelInstance(ExcelManager excelManager)
        {
            try
            {
                excelManager.StartExcelInstance();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartExcelInstance));
            }
        }

        private void DominatorsManager_ServerStatusChangedEvent(bool listening)
        {
            Listening = listening;
        }

        private void DominatorsManager_DominatorMessageEvent(string message, string title, bool silent)
        {
            if (silent)
            {
                Message = message;

                System.Timers.Timer timer = new(7000);
                timer.Elapsed += (t, e) => Message = "";
                timer.AutoReset = false;
                timer.Start();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBoxService?.ShowMessage(message,
                                                   title,
                                                   MessageButton.OK,
                                                   MessageIcon.Information);
                }));
            }
        }

        private void ModifyAll(bool updateEdgeMultiplier,
                               double edgeMultiplier,
                               bool updateDeltaMax,
                               double deltaMax,
                               bool updateLoopSize,
                               int loopSize,
                               bool updateDaysToExpiration,
                               int minDaysToExpiration,
                               int maxDaysToExpiration,
                               List<Tuple<int, double>> timeToEdgeSettings,
                               List<Tuple<double, double>> deltaToEdgeSettings,
                               List<DominatorModel> doms)
        {
            string updateCommand = "";

            if (updateEdgeMultiplier)
            {
                updateCommand += "EdgeMultiplier:" + edgeMultiplier + ";";
            }
            if (updateDeltaMax)
            {
                updateCommand += "DeltaMax:" + deltaMax + ";";
            }
            if (updateLoopSize)
            {
                updateCommand += "LoopSize:" + loopSize + ";";
            }
            if (updateDaysToExpiration && minDaysToExpiration <= maxDaysToExpiration)
            {
                updateCommand += "MinDaysToExpiration:" + minDaysToExpiration + ";";
                updateCommand += "MaxDaysToExpiration:" + maxDaysToExpiration + ";";
            }
            if (timeToEdgeSettings.Count > 0)
            {
                updateCommand += "GlobalCalendarEdge:";
                foreach (Tuple<int, double> pair in timeToEdgeSettings)
                {
                    updateCommand += pair.Item1 + "|" + pair.Item2 + ",";
                }
                updateCommand += ";";
            }
            if (deltaToEdgeSettings.Count > 0)
            {
                updateCommand += "LegSpreadDeltaEdge:";
                foreach (Tuple<double, double> pair in deltaToEdgeSettings)
                {
                    updateCommand += pair.Item1 + "|" + pair.Item2 + ",";
                }
                updateCommand += ";";
            }
            if (!string.IsNullOrEmpty(updateCommand))
            {
                foreach (DominatorModel item in doms)
                {
                    item.UpdateSettingsAsync(updateCommand);
                }
            }
        }

        internal void Dispose()
        {
            OmsCore.DominatorsManager.ServerStatusChangedEvent -= DominatorsManager_ServerStatusChangedEvent;
            OmsCore.DominatorsManager.DominatorMessageEvent -= DominatorsManager_DominatorMessageEvent;
        }

      
        public void OpenRestoreAutoTraderSettings(DominatorModel dominatorModel)
        {
            ConfigBrowserWindowView windowView = new();
            ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;
            windowView.Loaded += (_, _) => viewModel?.SetModule(Module.Dominator);
            viewModel.LoadConfig = async (configSave) =>
            {
                try
                {
                    var save = await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                    dominatorModel.OmsAutoTraderSettings = await viewModel.DeserializeConfigAsync<OmsAutoTraderSettings>(save);
                    dominatorModel.OmsAutoTraderSettings.Title = configSave.Title;
                    dominatorModel.OmsAutoTraderSettings = dominatorModel.OmsAutoTraderSettings;
                    await FetchRouteListAsync();
                }
                catch
                {
                    throw;
                }
            };
             
            windowView.ShowDialog();
        }

        async Task<List<ZPAccount>> GetAccountsAsync()
        {
            var accounts = await OmsCore.OrderClient.AccountsLookup.GetAccountsAsync(Clients.AccountsLookup.AccountsType.All);
            if (accounts == null || accounts.Count == 0)
            {
                accounts = await OmsCore.OrderClient.GetAccountAndRoutesAsync("");
            }
            return accounts;
        }
        public List<string> RoutesList { get; private set; }
        public List<string> DmaRoutesList { get; private set; }
        public List<string> SorRoutesList { get; private set; }
        Task FetchRouteListAsync()
        {
            var routeLookup = OmsCore.OrderClient?.RouteLookup;
            var classified = routeLookup?.GetClassifiedRoutes() ?? AutoTraderClient.ClassifiedRoutes.Empty;
            DmaRoutesList = classified.Dma.ToList();
            SorRoutesList = classified.Sor.ToList();
            RoutesList = classified.Combined.ToList();
            return Task.CompletedTask;
        }

        private static bool IsSorRoute(string route)
        {
            return OmsCore.OrderClient?.RouteLookup?.IsSmartRoute(route) ?? false;
        }

        private void OpenAutomationView<T>(DominatorModel dominatorModel) where T : UserControl, new()
        {
            dominatorModel.OmsAutoTraderSettings.AutomationConfigModel ??= new();
            var vm = new AutomationSettingsViewModel(nameof(DominatorsManagerViewModel), Module.DominatorsManager, OmsCore)
            {
                AutomationConfigModels = [dominatorModel.OmsAutoTraderSettings.AutomationConfigModel],
                AutomationConfig = dominatorModel.OmsAutoTraderSettings.AutomationConfigModel,
                RoutesList = RoutesList,
                DmaRoutesList = DmaRoutesList,
                SorRoutesList = SorRoutesList,
            };
            var view = new ThemedWindow
            {
                DataContext = vm,
                Content = new T(),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.WidthAndHeight,
            };
            view.Show();
        }

        
        public void OpenLooperSettings(DominatorModel dominatorModel)
        { 
            try
            {
                OpenAutomationView<LooperSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            }
        }

        
        public void OpeLegOutSettings(DominatorModel dominatorModel)
        {
            try
            {
                OpenAutomationView<LegOutSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpeLegOutSettings));
            }
        }

        
        public void OpenAutoHedgeSettings(DominatorModel dominatorModel)
        {
            try
            {
                OpenAutomationView<AutoHedgeSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenAutoHedgeSettings));
            }
        }

        
        public void OpenAutoLegSettings(DominatorModel dominatorModel)
        {
            try
            {
                OpenAutomationView<AutoLegSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenAutoLegSettings));
            }
        }

        
        public void OpenSweepSettings(DominatorModel dominatorModel)
        {
            try
            {
                OpenAutomationView<SweepTradeSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenSweepSettings));
            }
        }

       
        public void OpenRouteSettings(DominatorModel dominatorModel)
        {
            try
            {
                OpenAutomationView<RouteSettingsView>(dominatorModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenRouteSettings));
            }
        }

        
        public void OpenFishLossSettings(object commandParameter)
        {
            try
            {
                if (commandParameter is DominatorModel dominatorModel)
                {
                    FishLossPreventionSettingsView settingsView = new()
                    {
                        DataContext = new FishLossPreventionSettingsViewModel()
                        {
                            FishLossConfig = dominatorModel.OmsAutoTraderSettings.FishLossConfig,
                        }
                    };
                    var view = new ThemedWindow
                    {
                        Content = settingsView,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        SizeToContent = SizeToContent.WidthAndHeight,
                    };
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            }
        }

        public void OpenAutoCancelSettings(DominatorModel dominatorModel)
        {
            try
            {
                AutoCancelSettingsView settingsView = new()
                {
                    DataContext = new AutoCancelSettingsViewModel
                    {
                        AutoCancelConfig = dominatorModel.OmsAutoTraderSettings.AutoCancelConfig,
                    }
                };
                var view = new ThemedWindow
                {
                    Content = settingsView,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    SizeToContent = SizeToContent.WidthAndHeight,
                };
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            } 
        }

        public void SendSettingsToAutoTrader(object parameter)
        {
            if (parameter is DominatorModel dominatorModel)
            {
                DominatorsManager.SubmitAutoTraderSettings(dominatorModel);
                _log.Info("OmsAutoTraderSettings Send to AutoTrader from Dominator: {0}, with config Id: {1}", dominatorModel.Dominator.Id, dominatorModel.OmsAutoTraderSettings.ConfigId);
            }
            else _log.Error(nameof(SendSettingsToAutoTrader));
        }

        public bool CanConnectToAutoTrader(DominatorModel dominatorModel)
        {
            if (!OmsCore.AutoTraderClient.IsConnected) return false; 
            if (!dominatorModel.UseAutoTrader) return false;
            return true;
        }

        public void SaveAutoTraderConfig(DominatorModel dominatorModel)
        {
            try
            {
                SaveView view = new();
                SaveViewModel viewModel = view.DataContext as SaveViewModel;

                viewModel.LoadGroups(Module.Dominator);
                viewModel.Id = dominatorModel.OmsAutoTraderSettings.ConfigId.GetHashCode();
                viewModel.Title = dominatorModel.OmsAutoTraderSettings.Title;
                viewModel.Config = JsonConvert.SerializeObject(dominatorModel.OmsAutoTraderSettings, Formatting.Indented);


                view.ShowDialog();

                var save = new ConfigSave();
                if (viewModel.Success)
                {
                    save.Id = viewModel.Id;
                    save.Title = viewModel.Title;
                    save.Group = viewModel.SelectedGroup;
                    dominatorModel.OmsAutoTraderSettings.Title = viewModel.Title;
                    dominatorModel.OmsAutoTraderSettings = dominatorModel.OmsAutoTraderSettings;
                }

                if (viewModel.Success && viewModel.AddToFavorites)
                {
                    save.Id = viewModel.Id;
                    save.Title = viewModel.Title;
                    save.Group = viewModel.SelectedGroup;
                    save.ConfigJson = viewModel.Config;
                    OmsCore.Config.AddFavoriteModule(nameof(Module.Dominator), save);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAutoTraderConfig));
            }
        }
    }
}

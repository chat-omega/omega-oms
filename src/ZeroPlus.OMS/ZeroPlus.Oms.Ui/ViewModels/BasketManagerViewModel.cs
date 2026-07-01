using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    internal partial class BasketManagerViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private DelegateCommand<object> _checkAllWithSameValueCommand;

        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public BasketManagerModel BasketManager { get; }
        public string Uid { get; internal set; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial bool Listening { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Message { get; set; }

        public BasketManagerViewModel(BasketManagerModel basketManager)
        {
            BasketManager = basketManager;
            ModuleTitle = "Basket Manager";
            OmsCore.BasketManager.ServerStatusChangedEvent += BasketManager_ServerStatusChangedEvent;
            OmsCore.BasketManager.MessageEvent += BasketManager_MessageEvent;

            BasketManager_ServerStatusChangedEvent(OmsCore.BasketManager.Listening);
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
                    IEnumerable<BasketModel> rowsToActivate = null;
                    switch ((string)parameter.Field.ToString())
                    {
                        case nameof(BasketModel.Active):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.Active == parameter.Value);
                            break;
                        case nameof(BasketModel.ModuleTitle):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.ModuleTitle == parameter.Value);
                            break;
                        case nameof(BasketModel.Host):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.Host == parameter.Value);
                            break;
                        case nameof(BasketModel.Trader):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.Trader == parameter.Value);
                            break;
                        case nameof(BasketModel.Setup):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.Setup == parameter.Value);
                            break;
                        case nameof(BasketModel.List):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.List == parameter.Value);
                            break;
                        case nameof(BasketModel.RealizedPnl):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.RealizedPnl == parameter.Value);
                            break;
                        case nameof(BasketModel.AdjustedPnl):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.AdjustedPnl == parameter.Value);
                            break;
                        case nameof(BasketModel.State):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.State == parameter.Value);
                            break;
                        case nameof(BasketModel.Fills):
                            rowsToActivate = BasketManager.Baskets.ToList().Where(x => x.Fills == parameter.Value);
                            break;
                    }

                    if (rowsToActivate != null)
                    {
                        foreach (BasketModel item in rowsToActivate)
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
                BasketManagerView view = new();
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
                    viewModel.SetModule(Module.BasketManagerLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void CheckAll()
        {
            try
            {
                foreach (BasketModel item in BasketManager.Baskets)
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
        public void CancelAll()
        {
            try
            {
                foreach (BasketModel item in BasketManager.Baskets)
                {
                    _ = item.CancelAllAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAll));
            }
        }

        [Command]
        public void ResetAllTimers()
        {
            try
            {
                foreach (BasketModel item in BasketManager.Baskets)
                {
                    _ = item.ResetTimer();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetAllTimers));
            }
        }

        [Command]
        public void StopAllLoops()
        {
            try
            {
                foreach (BasketModel item in BasketManager.Baskets)
                {
                    _ = item.StopAllLoops();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllLoops));
            }
        }

        [Command]
        public void UncheckAll()
        {
            try
            {
                foreach (BasketModel item in BasketManager.Baskets)
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
        public void ActivateWindowCommand(BasketModel basketModel)
        {
            _ = basketModel.Activate();
        }

        [Command]
        public void HideWindowCommand(BasketModel basketModel)
        {
            _ = basketModel.Hide();
        }

        [Command]
        public void CloseWindowCommand(BasketModel basketModel)
        {
            _ = basketModel.Close();
        }

        [Command]
        public void RemoveCommand(BasketModel basketModel)
        {
            bool ok = MessageBoxService?.Show("Are you sure you want to close this basket?",
                                              "Verification",
                                              MessageButton.YesNo,
                                              MessageIcon.Warning,
                                              MessageResult.Yes) == MessageResult.Yes;
            if (ok)
            {
                _ = basketModel.Close();
            }
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

        private void BasketManager_ServerStatusChangedEvent(bool listening)
        {
            Listening = listening;
        }

        private void BasketManager_MessageEvent(string message, string title, bool silent)
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

        internal void Dispose()
        {

        }
    }
}

using NLog;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Modules
{
    public class ModuleFactory : IModuleFactory
    {
        protected static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsCore _omsCore;
        private readonly DispatcherStore _dispatcherStore;

        public ModuleFactory(OmsCore omsCore, DispatcherStore dispatcherStore)
        {
            _omsCore = omsCore;
            _dispatcherStore = dispatcherStore;
        }

        public ModuleWindow CreateModule(Module module, bool loadDefault = true)
        {
            return CreateModule(module, null, loadDefault);
        }

        public ModuleWindow CreateModule(Module module, string id, bool loadDefault = true)
        {
            switch (module)
            {
                case Module.LowLatencyOrderBook:
                case Module.EdgeScanFeedStatistics:
                case Module.WinningTradesMonitor:
                case Module.CloseSubsMonitor:
                case Module.ExecutionTransaction:
                    return CreateModuleInDispatcher(_dispatcherStore.GetDispatcherForModule(Module.OrderBook), module, id, loadDefault);
                case Module.EodRisk:
                case Module.Dashboard:
                case Module.UserPosition:
                case Module.MarketMovers:
                    return CreateModuleInDispatcher(_dispatcherStore.GetDispatcherForModule(Module.Portfolio), module, id, loadDefault);
                case Module.BasketTrader when OmsCore.Config.UseCommonDispatcherForBaskets:
                case Module.BasketGroup when OmsCore.Config.UseCommonDispatcherForBaskets:
                    return CreateModuleInDispatcher(_dispatcherStore.GetDispatcherForModule(Module.BasketTrader), module, id, loadDefault);
                default:
                    return CreateModuleInNewThread(module, id, loadDefault);
            }
        }

        public ModuleWindow CreateModule(Module module, string id, string config)
        {
            var window = CreateModule(module, id, false);

            if (config != null)
            {
                var moduleViewModel = window.ViewModel;
                if (moduleViewModel.IsReady)
                {
                    OnReady(moduleViewModel);
                }
                else
                {
                    moduleViewModel.Ready += OnReady;
                }

                void OnReady(IModuleViewModel moduleBase)
                {
                    moduleBase.Ready -= OnReady;
                    window.LoadConfigFromJsonAsync(config);
                }
            }

            return window;
        }

        public ModuleWindow CreateModule(Module module, string id, ConfigSave config)
        {
            var window = CreateModule(module, id);

            if (config != null)
            {
                var moduleViewModel = window.ViewModel;
                if (moduleViewModel.IsReady)
                {
                    OnReady(moduleViewModel);
                }
                else
                {
                    moduleViewModel.Ready += OnReady;
                }

                void OnReady(IModuleViewModel moduleBase)
                {
                    moduleBase.Ready -= OnReady;
                    window.RestoreFromConfigSaveAsync(config);
                }
            }

            return window;
        }

        private ModuleWindow CreateModuleInDispatcher(Dispatcher dispatcher, Module module, string id, bool loadDefault = true)
        {
            if (dispatcher == null)
            {
                _log.Error($"Creating Module in existing thread failed. Module: {module}, Dispather: null");
                return null;
            }

            _log.Info($"Creating Module in existing thread. Module: {module}, Dispather: {dispatcher.Thread.Name}");
            ModuleWindow window = null;
            dispatcher.Invoke(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));
                window = CreateWindow(module, id, loadDefault);
                window?.Show();
            });
            return window;
        }

        private ModuleWindow CreateModuleInNewThread(Module module, string id, bool loadDefault = true)
        {
            _log.Info($"Creating Module in new thread. Module: {module}");
            ModuleWindow window = null;
            ManualResetEventSlim manualResetEvent = new();
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                window = CreateWindow(module, id, loadDefault);

                if (window == null)
                {
                    return;
                }
                window.Loaded += OnWindowLoaded;
                window.Show();
                Dispatcher.Run();
            })
            {
                Name = module.ToString()
            };
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.Start();
            manualResetEvent.Wait();
            return window;

            void OnWindowLoaded(object sender, RoutedEventArgs e)
            {
                window.Loaded -= OnWindowLoaded;
                manualResetEvent.Set();
            }
        }

        public ModuleWindow CreateWindow(Module module, string id = null, bool loadDefault = true)
        {
            _log.Info($"Creating Window. Module: {module}, Id: {id}");
            switch (module)
            {
                case Module.EmaChart:
                    return new EmaChartView(this, id);
                case Module.ScriptTrader:
                    return new ScriptTraderView(this, id);
                case Module.LowLatencyManager:
                    return new LowLatencyManagerView(this, id);
                case Module.LowLatencyOrderBook:
                    return new LowLatencyOrderBookView(this, id);
                case Module.NewDominatorManager:
                    return new NewDominatorManager(this, id);
                case Module.EdgeScanFeed:
                    return new EdgeScanFeedView(this, id, loadDefault);
                case Module.BulletinBoard:
                    return new BulletinBoardView(this, id);
                case Module.MarketMovers:
                    return new MarketMoversView(this, id);
                case Module.BasketGroup:
                    return new BasketGroupView(this, id);
                case Module.BasketTrader:
                    return new BasketTraderView(_omsCore, this, id, loadDefault);
                case Module.ComplexOrderTicket:
                    return new ComplexOrderTicketView(this, id, loadDefault);
                case Module.EdgeScanFeedStatistics:
                    return new EdgeScanFeedStatisticsView(this, id);
                case Module.WinningTradesMonitor:
                    return new WinningTradesMonitorView(this, id);
                case Module.Dashboard:
                    return new DashboardView(this, id);
                case Module.EodRisk:
                    return new EodRiskView(this, id);
                case Module.UserPosition:
                    return new UserPositionView(this, id);
                case Module.LockTrader:
                    return new LockTraderView(this, id);
                case Module.GammaScalp:
                    return new GammaScalpView(this, id);
                case Module.CloseSubsMonitor:
                    return new CloseSubsMonitorView(this, id);
                case Module.ExecutionTransaction:
                    return new ExecutionTransactionsView(this, id);
                case Module.CobFeed:
                    return new CobFeedView(this, id);
                case Module.QuotesAndGreeksBoard:
                    return new QuotesAndGreeksBoardView(this, id);
                case Module.ExplorerWindow:
                    return new ExplorerWindowView(this, id);
                case Module.ImpliedQuoteFeed:
                    return new ImpliedQuotesFeedView(this, id);
                case Module.SpreadsGenerator:
                    return new SpreadsGeneratorView(_omsCore, this, id, loadDefault);
                case Module.CobOrders:
                    return new CobOrdersView(this, id);
                case Module.LiveVolData:
                    return new LiveVolDataView(this, id);
                case Module.AdminControls:
                    return new AdminControlsView(this, id);
            }
            return default;
        }

        public bool IsPersistentDispatcher(Dispatcher dispatcher)
        {
            return _dispatcherStore.IsPersistentDispatcher(dispatcher);
        }
    }
}

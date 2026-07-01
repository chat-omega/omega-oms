using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ReportBugViewModel : ViewModelBase
    {
        private static readonly string MODULE_TITLE = "Bug Report";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public List<string> Modules => Enum.GetNames(typeof(Module)).Select(x => GetModuleName(x)).Distinct().OrderBy(x => x).ToList();
        public List<string> Types => new() { "Bug", "Comment", "Feature Request" };
        public List<string> Severities => new() { "High", "Medium", "Low" };
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string SelectedModule { get; set; }

        [Bindable]
        public partial string SelectedType { get; set; }

        [Bindable]
        public partial string SelectedSeverity { get; set; }

        [Bindable]
        public partial string Subject { get; set; }

        [Bindable]
        public partial string Details { get; set; }

        public ReportBugViewModel()
        {
            ModuleTitle = MODULE_TITLE;
            SelectedModule = Modules.FirstOrDefault();
            SelectedType = Types.LastOrDefault();
            SelectedSeverity = Severities.LastOrDefault();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public async Task TakeScreenshot()
        {
            Module module = Enum.Parse<Module>(SelectedModule.Replace(" ", ""));
            switch (module)
            {
                case Module.OrderBook:
                case Module.OrderBookLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<OrderBookWindowView>();
                    break;
                case Module.CustomOrderBook:
                case Module.CustomOrderBookLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<EmptyOrderBookView>();
                    break;
                case Module.ComplexOrderTicket:
                case Module.ComplexOrderTicketLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ComplexOrderTicketView>();
                    break;
                case Module.CombinedOrderTicket:
                case Module.CombinedOrderTicketLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<CombinedOrderTicketView>();
                    break;
                case Module.BasketTrader:
                case Module.BasketTraderLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<BasketTraderView>();
                    break;
                case Module.Trades:
                case Module.TradesLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<TradesView>();
                    break;
                case Module.SpreadsGenerator:
                case Module.SpreadsGeneratorLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<SpreadsGeneratorView>();
                    break;
                case Module.DominatorsManager:
                case Module.DominatorsManagerLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<DominatorsManagerView>();
                    break;
                case Module.BasketManager:
                case Module.BasketManagerLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<BasketManagerView>();
                    break;
                case Module.LockTrader:
                case Module.LockTraderLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<LockTraderView>();
                    break;
                case Module.Heatmap:
                case Module.HeatmapLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<SpreadHeatmapView>();
                    break;
                case Module.Dashboard:
                case Module.DashboardLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<DashboardView>();
                    break;
                case Module.Portfolio:
                case Module.PortfolioLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<PortfolioView>();
                    break;
                case Module.OptionChain:
                case Module.OptionChainLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<OptionChainView>();
                    break;
                case Module.PnlReport:
                case Module.PnlReportLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<PnlReportView>();
                    break;
                case Module.Notification:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<NotificationItemView>();
                    break;
                case Module.SpreadTemplate:
                case Module.SpreadTemplateLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<SpreadTemplateView>();
                    break;
                case Module.PositionManager:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<PositionManagerView>();
                    break;
                case Module.ReleaseNotes:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ReleaseNotesView>();
                    break;
                case Module.ChangeLogs:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ChangeLogView>();
                    break;
                case Module.ReportBug:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ReportBugView>();
                    break;
                case Module.ExportSpreadsToFile:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ExportSpreadsToFileView>();
                    break;
                case Module.LoadDomList:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<LoadDomListView>();
                    break;
                case Module.ModifyStagedDoms:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ModifyStagedDomsView>();
                    break;
                case Module.ModifyStagedOrders:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ModifyStagedOrdersView>();
                    break;
                case Module.ModifyStagedOrdersPxQty:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ModifyStagedPxQtyView>();
                    break;
                case Module.ArchiveRequest:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ArchiveRequestView>();
                    break;
                case Module.ChartOrderBook:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ChartView>();
                    break;
                case Module.BrowseConfig:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ConfigBrowserWindowView>();
                    break;
                case Module.SaveConfig:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<SaveView>();
                    break;
                case Module.ShareConfig:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<ShareWithView>();
                    break;
                case Module.MainWindow:
                case Module.MainWindowLayout:
                    await StartupWindowViewModel.MainWindow.WindowHelper.GetScreenshotsForTypeAsync<MainView>();
                    break;
                case Module.Workspace:
                case Module.SmartRoutes:
                case Module.FishRoutes:
                    break;
            }
        }

        [Command]
        public void Submit()
        {
            OmsCore.GatewayClient.SendUserFeedback(SelectedModule, SelectedType, SelectedSeverity, Subject, Details);

            switch (SelectedType)
            {
                case "Bug":
                    MessageBoxService.ShowMessage("Thank you for your bug report!");
                    break;
                case "Comment":
                    MessageBoxService.ShowMessage("Thank you for your comment!");
                    break;
                case "Feature Request":
                    MessageBoxService.ShowMessage("Thank you for your feature request!");
                    break;
            }
            CurrentWindowService?.Close();
        }

        private static string GetModuleName(string x)
        {
            return Regex.Replace(x, "(\\B[A-Z])", " $1").Replace("Layout", "").Trim();
        }
    }
}

using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Microsoft.Diagnostics.Symbols;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    internal partial class LiveVolDataViewModel : ModuleViewModelBase
    {
        public override Module Module { get; protected set; } = Module.LiveVolData;

        private readonly IModuleFactory _moduleFactory;
        private CancellationTokenSource _refreshCts;
        private DispatcherTimer _timer;

        public int RefreshIntervalInMinutes => 5;
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IDialogService CustomRangeDialogService => GetService<IDialogService>("LiveVolCustomRangeDialogService");

        [Bindable(Default = true)]
        public partial bool IsAutoRefreshOn { get; set; }
        [Bindable]
        public partial string RefreshMsg { get; set; }
        [Bindable]
        public partial ObservableCollection<LiveVolDataModel> LatestLiveVolData { get; set; }
        [Bindable]
        public partial DateTime StartTime { get; set; }
        [Bindable]
        public partial DateTime EndTime { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        public LiveVolDataViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
        {
            _moduleFactory = moduleFactory;
            StartTime = DateTime.Today;
            EndTime = DateTime.Now;
            LatestLiveVolData = new ObservableCollection<LiveVolDataModel>();
            _refreshCts = new CancellationTokenSource();
            RefreshLiveVolData(_refreshCts.Token);
            InitializeTimer();
        }

        [Command]
        public void ManualRefreshData(CancellationToken token)
        {
            RefreshLiveVolData(token);
            RestartTimer();
        }

        [Command]
        public void LoadHistoricalData(CancellationToken token)
        {
            if (string.IsNullOrEmpty(Symbol))
            {
                 _log.Error("Cannot load historical data without a Symbol.");
            }
            else
            {
                var symbols = (Symbol ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (symbols.Count == 0)
                {
                    return;
                }
                RefreshLiveVolData(token);
                RestartTimer();
            }
        }
        public bool CanLoadHistoricalData(CancellationToken token)
        {
            return !string.IsNullOrWhiteSpace(Symbol);
        }
        public async void RefreshLiveVolData(CancellationToken token)
        {
            LatestLiveVolData.Clear();
            List<LiveVolDataModel> latestLiveVolDataList = [];
            if (!string.IsNullOrEmpty(Symbol))
            {
                latestLiveVolDataList = await OmsCore.LiveVolDataClient.Client.GetHistoricalLiveVolDataAsync(Symbol.ToUpper(), StartTime, EndTime, token);
            }
            else
            {
                latestLiveVolDataList = await OmsCore.LiveVolDataClient.Client.GetLatestLiveVolDataAsync(token);
            }
            if (token.IsCancellationRequested)
                return;
            LatestLiveVolData = new ObservableCollection<LiveVolDataModel>(latestLiveVolDataList ?? []);
            RefreshMsg = "Last refreshed at " + DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt");
        }

        internal void LoadHistoricalBySymbol(string symbol, DateTime startTime, DateTime endTime)
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            Symbol = symbol;
            StartTime = startTime;
            EndTime = endTime;
            if (EndTime < DateTime.Now.AddMinutes(-1))
            {
                IsAutoRefreshOn = false;
            }
            RefreshLiveVolData(_refreshCts.Token);
            RestartTimer();
        }

        public async Task OpenHistoricalDataAsync(string symbol, DateTime startTime, DateTime endTime)
        {
            try
            {
                if (_moduleFactory.CreateModule(Module.LiveVolData) is LiveVolDataView { ViewModel: LiveVolDataViewModel viewModel } view)
                {
                    await view.ModuleLoadTask;
                    await view.Dispatcher.InvokeAsync(() => viewModel.LoadHistoricalBySymbol(symbol, startTime, endTime));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenHistoricalDataAsync));
            }
        }
        [Command]
        public async Task OpenHistoricalDataTodayCommand(LiveVolDataModel row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Symbol))
            {
                return;
            }

            await OpenHistoricalDataAsync(row.Symbol, DateTime.Today, DateTime.Now);
        }
        [Command]
        public async Task OpenHistoricalData24hCommand(LiveVolDataModel row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Symbol))
            {
                return;
            }

            await OpenHistoricalDataAsync(row.Symbol, DateTime.Now.AddDays(-1), DateTime.Now);
        }
        [Command]
        public async Task OpenHistoricalData7DaysCommand(LiveVolDataModel row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Symbol))
            {
                return;
            }

            await OpenHistoricalDataAsync(row.Symbol, DateTime.Today.AddDays(-7), DateTime.Now);
        }

        [Command]
        public async Task OpenHistoricalDataCustomRangeCommand(LiveVolDataModel row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Symbol))
            {
                return;
            }

            var viewModel = new LiveVolCustomRangeViewModel
            {
                Symbol = row.Symbol,
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Now
            };

            MessageResult result = CustomRangeDialogService.ShowDialog(
                dialogButtons: MessageButton.OKCancel,
                viewModel: viewModel,
                title: viewModel.Title);

            if (result == MessageResult.OK)
            {
                DateTime start = viewModel.StartDate.Date;
                DateTime end = viewModel.EndDate.Date.AddDays(1).AddTicks(-1);
                if (end > DateTime.Now)
                    end = DateTime.Now;
                await OpenHistoricalDataAsync(row.Symbol, start, end);
            }
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMinutes(RefreshIntervalInMinutes);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void RestartTimer()
        {
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_isAutoRefreshOn)
            {
                if (!string.IsNullOrEmpty(Symbol))
                {
                    EndTime = DateTime.Now;
                }
                RefreshLiveVolData(CancellationToken.None);
            }
        }

        public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return Task.CompletedTask;
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return default;
        }
    }
}

using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class QuotesAndGreeksBoardViewModel : ModuleViewModelBase
{
    private BlockingCollection<string> _csvQueue = new();
    private CancellationTokenSource _cts = new();
    private Task _writerTask;
    private CancellationTokenSource _intervalFlushCts;
    private Task _intervalFlushTask;
    private int _hasChangesSinceLastInterval;
    private readonly object _intervalSnapshotSync = new();
    private readonly List<IQuotesAndGreeks> _intervalSnapshotBuffer = [];
    private volatile bool _intervalSnapshotDirty = true;

    public override Module Module { get; protected set; } = Module.QuotesAndGreeksBoard;
    public FastObservableCollection<IQuotesAndGreeks> Updates { get; } = [];
    public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();

    [Bindable]
    public partial int LoggedCount { get; set; }

    private bool _useTickByTickInterval = true;
    public bool UseTickByTickInterval
    {
        get => _useTickByTickInterval;
        set
        {
            if (!SetValue(ref _useTickByTickInterval, value))
            {
                return;
            }

            ConfigureIntervalFlushLoop();
        }
    }

    private int _intervalSeconds = 60;
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            if (!SetValue(ref _intervalSeconds, value))
            {
                return;
            }

            ConfigureIntervalFlushLoop();
        }
    }

    public IDocumentManagerService ChartDocumentManagerService => GetService<IDocumentManagerService>("ChartWindowService");

    [Command]
    public void ShowChartCommand(IQuotesAndGreeks model)
    {
        if (model == null) return;

        var viewModel = new TimeSeriesChartViewModel(OmsCore);
        // Fire and forget initialization
        _ = viewModel.Initialize(model.Symbol, FilePath);

        IDocument document = ChartDocumentManagerService.CreateDocument("TimeSeriesChartView", viewModel);
        document.Title = $"Chart - {model.Symbol}";
        document.DestroyOnClose = true;
        document.Show();
    }

    [Bindable]
    public partial int SavedCount { get; set; }
    [Bindable]
    public partial string FilePath { get; set; }
    [Bindable]
    public partial bool IsRunning { get; set; }

    public QuotesAndGreeksBoardViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
    {
        Updates.CollectionChanged += OnUpdatesCollectionChanged;
    }

    [Command]
    public void AddCommand()
    {
        LoadSymbolView view = new LoadSymbolView();
        view.ShowDialog();
        if (view.DataContext is LoadSymbolViewModel { IsValid: true } viewModel)
        {
            IQuotesAndGreeks model = viewModel.LegsCount > 1 ? new SpreadQuotesAndGreeksModel(OmsCore) : new QuotesAndGreeksModel(OmsCore);
            model.Initialize(viewModel.Symbol);
            model.Updated += OnModelUpdate;
            Updates.Add(model);

            if (IsRunning)
            {
                model.Subscribe();
            }
        }
    }

    [Command]
    public void StartCommand()
    {
        SaveFileDialogService.DefaultExt = "csv";
        SaveFileDialogService.DefaultFileName = $"Quotes_And_Greeks_{DateTime.Now:yy-MM-ddThhmmss}";
        SaveFileDialogService.Filter = "csv|*.csv";
        bool dialogResult = SaveFileDialogService.ShowDialog();
        if (dialogResult)
        {
            FilePath = SaveFileDialogService.GetFullFileName();
            LoggedCount = 0;
            SavedCount = 0;

            if (!File.Exists(FilePath))
            {
                File.Create(FilePath);
            }

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ExportToFileHandler(FilePath, _cts.Token), _cts.Token);
            _csvQueue = new BlockingCollection<string>();
            _csvQueue.Add(IQuotesAndGreeks.ToCsvHeader());
            LoggedCount++;

            foreach (var model in Updates)
            {
                model.Subscribe();
            }

            IsRunning = true;
            ConfigureIntervalFlushLoop();
        }
    }

    [Command]
    public async void StopCommand()
    {
        await StopIntervalFlushLoopAsync();
        await _cts.CancelAsync();

        IsRunning = false;

        foreach (var model in Updates)
        {
            model.Unsubscribe();
        }

        if (_writerTask != null)
        {
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Command]
    public void RemoveCommand(IQuotesAndGreeks model)
    {
        model.Unsubscribe();
        model.Updated -= OnModelUpdate;
        Updates.Remove(model);
    }

    [Command]
    public void OpenFileCommand()
    {
        if (File.Exists(FilePath))
        {
            var psi = new ProcessStartInfo
            {
                FileName = FilePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }

    public void OnModelUpdate(IQuotesAndGreeks model, SubscriptionFieldType type, byte modelId)
    {
        if (!UseTickByTickInterval)
        {
            Interlocked.Exchange(ref _hasChangesSinceLastInterval, 1);
            return;
        }

        _csvQueue.Add(model.ToCsv());
        LoggedCount++;
    }

    private void ConfigureIntervalFlushLoop()
    {
        if (!IsRunning)
        {
            return;
        }

        if (UseTickByTickInterval)
        {
            _ = StopIntervalFlushLoopAsync();
            return;
        }

        _ = StartIntervalFlushLoopAsync();
    }

    private async Task StartIntervalFlushLoopAsync()
    {
        await StopIntervalFlushLoopAsync();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _intervalFlushCts = linkedCts;
        _intervalFlushTask = Task.Run(() => IntervalFlushLoopAsync(linkedCts.Token), linkedCts.Token);
    }

    private async Task StopIntervalFlushLoopAsync()
    {
        var cts = _intervalFlushCts;
        var task = _intervalFlushTask;

        _intervalFlushCts = null;
        _intervalFlushTask = null;

        if (cts == null)
        {
            return;
        }

        try
        {
            await cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // No-op if it is already disposed.
        }

        if (task != null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected when timer loop is cancelled.
            }
            catch (TimeoutException)
            {
                // Avoid blocking UI thread indefinitely.
            }
        }

        cts.Dispose();
    }

    private async Task IntervalFlushLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, IntervalSeconds)), token);

            if (Interlocked.Exchange(ref _hasChangesSinceLastInterval, 0) == 0)
            {
                continue;
            }

            EnqueueAllModelsForIntervalSnapshot();
        }
    }

    private void OnUpdatesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        lock (_intervalSnapshotSync)
        {
            RebuildIntervalSnapshotBufferUnsafe();
            _intervalSnapshotDirty = false;
        }
    }

    private void RebuildIntervalSnapshotBufferUnsafe()
    {
        _intervalSnapshotBuffer.Clear();
        var updatesCount = Updates.Count;
        for (var index = 0; index < updatesCount; index++)
        {
            _intervalSnapshotBuffer.Add(Updates[index]);
        }
    }

    private void EnqueueAllModelsForIntervalSnapshot()
    {
        lock (_intervalSnapshotSync)
        {
            if (_intervalSnapshotDirty)
            {
                RebuildIntervalSnapshotBufferUnsafe();
                _intervalSnapshotDirty = false;
            }

            if (_intervalSnapshotBuffer.Count == 0)
            {
                return;
            }

            foreach (var model in _intervalSnapshotBuffer)
            {
                _csvQueue.Add(model.ToCsv());
                LoggedCount++;
            }
        }
    }

    private async Task ExportToFileHandler(string filePath, CancellationToken token)
    {
        const int batchSize = 100;
        var errors = 0;
        var batch = new List<string>(batchSize);
        while (!token.IsCancellationRequested)
        {
            batch.Clear();
            try
            {
                if (_csvQueue.TryTake(out var firstItem, 5000, token))
                {
                    batch.Add(firstItem);
                }
                else
                {
                    continue;
                }

                while (batch.Count < batchSize && _csvQueue.TryTake(out var item))
                {
                    batch.Add(item);
                }

                await AppendLinesWithRetryAsync(filePath, batch, token);
                SavedCount += batch.Count;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportToFileHandler));
                if (errors++ > 20)
                {
                    break;
                }
            }
        }
    }

    public static async Task AppendLinesWithRetryAsync(string filePath, List<string> batch, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                await using var writer = new StreamWriter(stream);
                foreach (var line in batch)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    await writer.WriteLineAsync(line);
                }

                break;
            }
            catch (IOException ex)
            {
                _log.Error(ex, nameof(AppendLinesWithRetryAsync));
                await Task.Delay(1000, token);
            }
        }
    }

    public override void OnDispose()
    {
        Updates.CollectionChanged -= OnUpdatesCollectionChanged;
        StopCommand();
    }

    public void LoadSymbols(List<string> spreads)
    {
        var models = new List<IQuotesAndGreeks>();

        foreach (var symbol in spreads)
        {
            if (symbol != null)
            {
                IQuotesAndGreeks model = symbol.Length > 20 ? new SpreadQuotesAndGreeksModel(OmsCore) : new QuotesAndGreeksModel(OmsCore);
                model.Initialize(symbol);
                model.Updated += OnModelUpdate;
                models.Add(model);

                if (IsRunning)
                {
                    model.Subscribe();
                }
            }
        }

        if (models.Any())
        {
            Dispatcher.BeginInvoke(() => Updates.AddRange(models));
        }
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        QuotesAndGreeksConfig config = new()
        {
            LoadedSymbols = [.. Updates.Select(d => d.Symbol).Distinct()],
        };
        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        QuotesAndGreeksConfig config = JsonConvert.DeserializeObject<QuotesAndGreeksConfig>(configJson);
        LoadSymbols(config.LoadedSymbols);

        return Task.CompletedTask;
    }
}
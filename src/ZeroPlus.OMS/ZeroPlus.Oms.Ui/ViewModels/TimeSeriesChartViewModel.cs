using DevExpress.Mvvm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class TimeSeriesChartViewModel : ViewModelBase, IOmsDataSubscriber, IDisposable
{
    private readonly OmsCore _omsCore;
    private TimeSpan _candleInterval = TimeSpan.FromMinutes(1);
    private BarModel _currentCandle;

    public bool IsDisposed { get; set; }

    [Bindable]
    public partial string Symbol { get; set; }

    public FastObservableCollection<BarModel> OhlcPoints { get; } = new();

    public TimeSeriesChartViewModel(OmsCore omsCore)
    {
        _omsCore = omsCore;
    }

    public async Task Initialize(string symbol, string csvFilePath)
    {
        Symbol = symbol;

        if (!string.IsNullOrEmpty(csvFilePath) && File.Exists(csvFilePath))
        {
            await LoadFromCsv(csvFilePath);
        }

        // Subscribe to real-time updates
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
    }

    private async Task LoadFromCsv(string filePath)
    {
        try
        {
            var points = new List<BarModel>();

            // Read all lines - using FileStream with FileShare.ReadWrite to avoid locking issues with the logger
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var header = await reader.ReadLineAsync();
            if (header == null) return;

            var columnMap = header.Split(',').Select((name, index) => new { name, index }).ToDictionary(x => x.name, x => x.index);

            int symIdx = columnMap.GetValueOrDefault("Symbol", -1);
            int timeIdx = columnMap.GetValueOrDefault("Time", -1);
            int midIdx = columnMap.GetValueOrDefault("Mid", -1);

            if (symIdx == -1 || timeIdx == -1 || midIdx == -1) return;

            DateTime? lastCandleTime = null;
            BarModel currentLoadCandle = null;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = line.Split(',');
                if (fields.Length <= Math.Max(symIdx, Math.Max(timeIdx, midIdx))) continue;

                if (fields[symIdx] != Symbol) continue;

                if (!DateTime.TryParse(fields[timeIdx], out var time)) continue;
                if (!double.TryParse(fields[midIdx], out var price)) continue;

                var bucketTime = TruncateTo(time, _candleInterval);

                if (currentLoadCandle == null || bucketTime > lastCandleTime)
                {
                    currentLoadCandle = new BarModel { Time = bucketTime, Open = price, High = price, Low = price, Close = price };
                    points.Add(currentLoadCandle);
                    lastCandleTime = bucketTime;
                }
                else
                {
                    currentLoadCandle.High = Math.Max(currentLoadCandle.High, price);
                    currentLoadCandle.Low = Math.Min(currentLoadCandle.Low, price);
                    currentLoadCandle.Close = price;
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                OhlcPoints.AddRange(points);
                _currentCandle = OhlcPoints.LastOrDefault();
            });
        }
        catch (Exception ex)
        {
            // Log error
            System.Diagnostics.Debug.WriteLine($"Error loading CSV for chart: {ex.Message}");
        }
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
    {
        if (key.Symbol != Symbol) return;

        double? price = null;
        if (key.Type == SubscriptionFieldType.Bid && value is double bid) price = bid;
        else if (key.Type == SubscriptionFieldType.Ask && value is double ask) price = ask;

        if (price.HasValue)
        {
            UpdateChart(DateTime.Now, price.Value);
        }
    }

    private void UpdateChart(DateTime timestamp, double price)
    {
        var bucketTime = TruncateTo(timestamp, _candleInterval);

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_currentCandle == null || bucketTime > _currentCandle.Time)
            {
                _currentCandle = new BarModel { Time = bucketTime, Open = price, High = price, Low = price, Close = price };
                OhlcPoints.Add(_currentCandle);
            }
            else
            {
                _currentCandle.High = Math.Max(_currentCandle.High, price);
                _currentCandle.Low = Math.Min(_currentCandle.Low, price);
                _currentCandle.Close = price;
            }
        });
    }

    private static DateTime TruncateTo(DateTime dt, TimeSpan ts)
    {
        if (ts == TimeSpan.Zero) return dt;
        return new DateTime(dt.Ticks - (dt.Ticks % ts.Ticks), dt.Kind);
    }

    public void OnDispose()
    {
        _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
        IsDisposed = true;
    }

    public void Dispose()
    {
        OnDispose();
        GC.SuppressFinalize(this);
    }
}

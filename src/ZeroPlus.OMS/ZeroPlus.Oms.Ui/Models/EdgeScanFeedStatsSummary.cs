using DevExpress.Mvvm;
using System;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Models;

public partial class EdgeScanFeedStatsSummary : BindableBase
{
    string _instanceId;

    internal EdgeScanFeedStatsModel Model { get; } = new();

    [Bindable]
    public partial string InstanceId { get; set; }
    [Bindable]
    public partial string State { get; set; }
    [Bindable]
    public partial string User { get; set; }
    [Bindable]
    public partial string ScannerConfig { get; set; }
    [Bindable]
    public partial string BasketConfig { get; set; }
    [Bindable]
    public partial int TotalAttempts { get; set; }
    [Bindable]
    public partial int TotalSubs { get; set; }
    [Bindable]
    public partial DateTime StartTime { get; set; }
    [Bindable]
    public partial int Submissions { get; set; }
    [Bindable]
    public partial int Received { get; set; }
    [Bindable]
    public partial DateTime Timestamp { get; set; }
    [Bindable]
    public partial double WinLoseRatio { get; set; }
    [Bindable]
    public partial int WinningTradesCount { get; set; }
    [Bindable]
    public partial int LosingTradesCount { get; set; }

    public List<EdgeScanFeedStatsSummary> Self { get; }

    public EdgeScanFeedStatsSummary()
    {
        Self = [this];
    }

    public void Update()
    {
        InstanceId = Model.InstanceId;
        State = Model.State;
        User = Model.User;
        ScannerConfig = Model.ScannerConfig;
        BasketConfig = Model.BasketConfig;
        TotalAttempts = Model.TotalAttempts;
        TotalSubs = Model.TotalSubs;
        Submissions = Model.Submissions;
        Received = Model.Received;
        StartTime = Model.StartTime;
        Timestamp = Model.Timestamp;
        WinLoseRatio = Model.WinLoseRatio;
        WinningTradesCount = Model.WinningTradesCount;
        LosingTradesCount = Model.LosingTradesCount;
    }
}
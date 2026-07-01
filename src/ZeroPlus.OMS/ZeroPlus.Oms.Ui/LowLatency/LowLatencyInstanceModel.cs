using System.Collections.Generic;
using DevExpress.Mvvm;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.LowLatency;

public class LowLatencyInstanceModel : BindableBase
{
    public string Title { get; set; }
    public FastObservableCollection<LowLatencyOrderModel> WorkingOrders { get; } = new();
    public FastObservableCollection<LowLatencyOrderModel> Orders { get; } = new();
    public List<LowLatencyOrderModel> Buffer { get; } = new();
}
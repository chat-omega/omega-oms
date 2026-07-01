using System;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Models;

[Serializable]
public class BasketDragItem
{
    public Dispatcher Dispatcher { get; set; }
    public BasketTraderViewModel ViewModel { get; set; }
    public BasketTraderView Window { get; set; }
    public string ConfigAsJson { get; set; }
}
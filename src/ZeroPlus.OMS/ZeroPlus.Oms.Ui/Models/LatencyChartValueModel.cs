using DevExpress.Mvvm;
using NLog;
using System;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LatencyChartValueModel : BindableBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public Dispatcher Dispatcher { get; internal set; }

        private double _Title;
        public double Title
        {
            get => _Title;
            set => SetValue(ref _Title, Math.Round(value, 2));
        }

        [Bindable]
        public partial double Value { get; set; }
    }
}

using DevExpress.Mvvm;
using NLog;
using System;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LiveChartValueModel : BindableBase, IOmsDataSubscriber
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private ChartField _field;

        protected OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Dispatcher Dispatcher { get; internal set; }
        public bool IsDisposed { get; set; }
        internal double LatestValue { get; set; }

        private double _Title;
        public double Title
        {
            get => _Title;
            set => SetValue(ref _Title, Math.Round(value, 2));
        }

        [Bindable]
        public partial double Value { get; set; }

        public string Uid { get; internal set; }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (value is double update)
                {
                    LatestValue = update;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        internal void Initialize(Option option, ChartField selectedChartField)
        {
            Title = option.Strike;
            _field = selectedChartField;
            switch (selectedChartField)
            {
                case ChartField.Iv:
                    OmsCore.GreekClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.ImpliedVol, this);
                    break;
                case ChartField.Theo:
                    OmsCore.GreekClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.TheorethicalValue, this);
                    break;
                case ChartField.AdjTheo:
                    OmsCore.UpdateManager.Subscribe(option.OptionSymbol, SubscriptionFieldType.DeltaAdjTheo, this);
                    break;
            }
        }

        internal void Update()
        {
            try
            {
                Value = LatestValue;
            }
            catch (Exception) { }
        }

        internal void Dispose()
        {
            try
            {
                switch (_field)
                {
                    case ChartField.Iv:
                        OmsCore.GreekClient.UnsubscribeAll(this);
                        break;
                    case ChartField.Theo:
                        OmsCore.GreekClient.UnsubscribeAll(this);
                        break;
                    case ChartField.AdjTheo:
                        OmsCore.UpdateManager.UnsubscribeAll(this);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }
    }
}

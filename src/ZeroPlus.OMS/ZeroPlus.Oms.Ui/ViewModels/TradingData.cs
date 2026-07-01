using DevExpress.Mvvm;
using System;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TradingData : BindableBase
    {
        private double _open;
        private double _close;

        public bool UpdateSuspended { get; private set; }
        public DateTime Date { get; set; }
        public double Open
        {
            get => _open;
            set
            {
                if (_open != value)
                {
                    SetValue(ref _open, value);
                    UpdateVolumeColor();
                }
            }
        }
        [Bindable]
        public partial double High { get; set; }
        [Bindable]
        public partial double Low { get; set; }
        public double Close
        {
            get => _close;
            set
            {
                if (_close != value)
                {
                    SetValue(ref _close, value);
                    UpdateVolumeColor();
                }
            }
        }
        [Bindable]
        public partial double Volume { get; set; }
        [Bindable]
        public partial Color VolumeColor { get; set; }

        public TradingData(DateTime date, double open, double high, double low, double close, double volume)
        {
            Date = date;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            UpdateVolumeColor();
        }

        private void UpdateVolumeColor()
        {
            VolumeColor = _close >= _open ? (Color)App.Current.Resources["lightGreenColor"] : (Color)App.Current.Resources["lightRedColor"];
        }

        public void SuspendUpdate()
        {
            UpdateSuspended = false;
        }

        public void ResumeUpdate()
        {
            UpdateSuspended = false;
        }
    }
}

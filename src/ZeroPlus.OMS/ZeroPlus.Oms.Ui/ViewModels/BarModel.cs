using System;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class BarModel
    {
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public double VolumeCache { get; internal set; }
        public DateTime Time { get; internal set; }
    }
}

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class EmaChartConfig : ModuleConfigBase
    {
        public string Symbol { get; set; }
        public double EmaSmoothing { get; set; } = 2.0;
        public double EmaInterval { get; set; } = 5000;
        public double EmaPeriods { get; set; } = 20;
        public double Ema2Smoothing { get; set; } = 2.0;
        public double Ema2Interval { get; set; } = 5000;
        public double Ema2Periods { get; set; } = 50;
        public double Ema3Smoothing { get; set; } = 2.0;
        public double Ema3Interval { get; set; } = 5000;
        public double Ema3Periods { get; set; } = 100;
        public double SignalEmaSmoothing { get; set; } = 2.0;
        public double SignalEmaInterval { get; set; } = 5000;
        public double SignalEmaPeriods { get; set; } = 7;
        public double SlowEmaSmoothing { get; set; } = 2.0;
        public double SlowEmaInterval { get; set; } = 5000;
        public double SlowEmaPeriods { get; set; } = 21;
        public double FastEmaSmoothing { get; set; } = 2.0;
        public double FastEmaInterval { get; set; } = 5000;
        public double FastEmaPeriods { get; set; } = 14;
        public bool ShowBid { get; set; }
        public bool ShowMid { get; set; } = true;
        public bool ShowAsk { get; set; }
        public bool ShowBidEma { get; set; }
        public bool ShowMidEma { get; set; } = true;
        public bool ShowMidEma2 { get; set; }
        public bool ShowMidEma3 { get; set; }
        public bool ShowAskEma { get; set; }
        public bool ShowHighestBid { get; set; }
        public bool ShowLowestAsk { get; set; }
        public bool ShowMacd { get; set; } = true;
        public bool LoadHistoric { get; set; } = true;
        public int HistoricRequestDays { get; set; }
        public int BarInterval { get; set; } = 5000;
    }
}

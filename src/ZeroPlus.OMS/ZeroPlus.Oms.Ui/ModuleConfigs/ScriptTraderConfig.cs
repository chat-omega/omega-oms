using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ScriptTraderConfig : ModuleConfigBase
    {
        public string Symbol { get; set; }
        public string ReverseSymbol { get; set; }
        public int Qty { get; set; }
        public ScriptTradeType ScriptTradeType { get; set; }
        public string EntryScript { get; set; }
        public string ExitScript { get; set; }
        public string StopLossScript { get; set; }
        public ScriptTriggerType EntryScriptTriggerType { get; set; }
        public ScriptTriggerType ExitScriptTriggerType { get; set; }
        public ScriptTriggerType StopLossScriptTriggerType { get; set; }
        public int EntryInterval { get; set; }
        public int ExitInterval { get; set; }
        public int StopLossInterval { get; set; }
        public bool LoadHistoric { get; set; }
        public bool SubscribeToQuotes { get; set; }
        public bool SubscribeToGreeks { get; set; }
        public bool SubscribeToEma { get; set; }
        public bool SubscribeToMacd { get; set; }
        public int MaxPos { get; set; }
        public double MaxUnreal { get; set; }
        public double MaxRest { get; set; }
        public double PayThroughMkt { get; set; }
        public InitSide StockPairBuyInitialSide { get; set; }
        public InitSide StockPairSellInitialSide { get; set; }
        public ExecutionStyle StockPairBuyExecStyle { get; set; }
        public ExecutionStyle StockPairSellExecStyle { get; set; }
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
    }
}

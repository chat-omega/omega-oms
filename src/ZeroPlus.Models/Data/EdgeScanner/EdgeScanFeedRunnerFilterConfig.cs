using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class EdgeScanFeedRunnerFilterConfig
    {
        public string? FilterString { get; set; }
        public string? FilterConfig { get; set; }
        public int FilterConfigId { get; set; }

        public bool AutoTraderSkipActiveOrders { get; set; } = true;
        public AutoTraderEdgeOverride AutoTraderEdgeOverride { get; set; }
        public AutoTraderSideSelection AutoTraderSideSelector { get; set; }
        public bool AutoTraderUseTradePrice { get; set; }
        public bool AutoTraderAttemptBothSides { get; set; } = true;
        public bool AutoTraderDoNotTradeThroughFillPrice { get; set; }
        public int AutoTraderMinQty { get; set; }
        public AutoTraderRouteOption AutoTraderRouteOption { get; set; }
        public int AutoTraderMaxLatency { get; set; }
        public int AutoTraderMaxOpenPos { get; set; } = 2;
        public int AutoTraderResubmitCount { get; set; } = 0;
        public int AutoTraderMaxAllowedOrders { get; set; } = 10_000;
        public int AutoTraderMaxOrderRate { get; set; } = 1_000;
        public bool AutoTraderEnablePayUpTicks { get; set; }
        public int AutoTraderPayUpTicks { get; set; }

        public bool BlockAlreadyTradedSymbols { get; set; }
        public double BlockAlreadyTradedSymbolsTimeout { get; set; } = 1500;
        public bool BlockFirmTradesForTime { get; set; }
        public int BlockFirmTradesForTimeInterval { get; set; }
        public bool BlockArea { get; set; }
        public double BlockAreaStrikeRange { get; set; }

        public DateTime CutoffTime { get; set; } = DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(12);
        public bool AutoStop { get; set; } = true;

        public bool MarkPrices { get; set; }
        public double MarkPricesMinEdge { get; set; }
        public Dictionary<string, string> ExchToRouteMapV3 { get; set; } = new()
        {
            ["ISE"] = "BISE",
            ["CBOE"] = "BCBOE",
            ["PHLX"] = "BPHLX",
            ["ARCA"] = "BARCA",
            ["BOX"] = "BBOX",
            ["MIAX"] = "BMIAX",
            ["C2"] = "BC2",
            ["EDGX"] = "BEDGX",
            ["AMEX"] = "BAMEX",
            ["EMLD"] = "BEMLD",
            ["MCRY"] = "BMCRY",
            ["NOM"] = "BNASDAQ",
            ["BATS"] = "BBATS",
            ["U"] = "IMEMX",
            ["NQBX"] = "BNQBX",
            ["GEMX"] = "BGMNI",
            ["MPRL"] = "BPEARL",
            ["S"] = "ISPHR",
        };
        public bool MinPnlForAutoTraderEnabled { get; set; }
        public double MinPnlForAutoTrader { get; set; } = -.05;
        public bool MinPnlMaxQtyCheckEnabled { get; set; }
        public int MinPnlMaxQty { get; set; }
    }
}

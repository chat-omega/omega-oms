using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.LowLatency.Ext
{
    public class MsgRequests
    {
        public class jsonRequestLeanBaseParams
        {
            public string MinSpreadPrice;
            public string MaxSpreadPrice;
            public string MaxSideSpreadPrice;
            public string MinNbboPrice;
            public string MaxNbboPrice;
            public int MinL1LeanQty;
            public int MinL1LeanCnt;
            public int MinL2LeanQty;
            public int MinL2LeanCnt;
            public int MinSideQty;
            public int MinDigQty;
            public int MinDigCnt;
            public int UseDig;
        }

        public class jsonRequestParamWatchlist
        {
            public string WatchlistName;
            public int WatchlistSymbolsCount;
            public List<string> WatchlistSymbols;
        }

        public class jsonRequestRunTrade
        {
            public string ParamTradeName;
            //public int OrderQty;
            //public byte EnterSide;
            public string InitiatorExchange;
            public string LiquidatorExchange;
        }

        public class jsonRequestParamTrades
        {
            public string ParamTradeName;
            public string ParamBasketName;
            public string ParamWatchlistName;
        }

        public class jsonRequestExecutionTrailerParams // GOT
        {
            public int TTLTrail;
            public int PukeOnExit;
            public int ThruTicks;
            public int Range0;
            public int TrailTicks0;
            public int Range1;
            public int TrailTicks1;
            public int Range2;
            public int TrailTicks2;
            public int Range3;
            public int TrailTicks3;
            public int Range4;
            public int TrailTicks4;
        }

        public class jsonRequestExecutionHunterParams
        {
            public int HuntTTL;
            public int WorkTTL;
            public int PayupTicks;
            public int UseSignalPrice;
            public int LiquidateOnlyWhenComplete;
            public int UseSignalExchange;
            public int LoopDelayTTL;
            public int LoopPayupQty0;
            public int LoopPayupQty1;
            public int LoopPayupQty2;
            public int LoopPayupQty3;
            public int LoopPayupQty4;
            public int LoopPayupTicks0;
            public int LoopPayupTicks1;
            public int LoopPayupTicks2;
            public int LoopPayupTicks3;
            public int LoopPayupTicks4;
            public string LoopProfit0;
            public string LoopProfit1;
            public string LoopProfit2;
            public string LoopProfit3;
            public string LoopProfit4;
            public jsonRequestLeanBaseParams Lean;
        }

        public class jsonRequestExecutionChaserParams // GOT
        {
            public int DelayMs;
            public int OffsetTicks;
            public int OffsetTTL;
            public int ChaseTTL;
            public int ScratchTTL;
            public int TradeOutTTL;
            public int TradeOutSweepTTL;
            public int TradeOutTicks;
            public int RollMode;
            public int SweepOnTradeoutMode;
            public int AdjTheoMode;
            public int MaxDupRetries;
            public string ChasePrice;
            public string SpookPrice;
            public string PayupTheoPrice;
        }

        public class jsonRequestExecutionBracketProfitParams // GOT
        {
            public int ProfitTicks;
        }

        public class jsonRequestExecutionBracketParams // GOT
        {
            public int TTLBracket;
            public int PukeOnExit;
            public jsonRequestExecutionBracketProfitParams Profit;
            public jsonRequestExecutionBracketLossParams Loss;
        }

        public class jsonRequestExecutionBracketLossParams
        {
            public int LossTicks;
            public int ThruTicks;
            public jsonRequestLeanBaseParams Lean;
        }

        /// ///

        public class jsonRequestRiskParams
        {
            public int MaxLossInDollars;
            public int MaxLossInDollarsLiq;
            public int MaxOpenPos;
            public int MaxOpenSymbols;
        }

        public class jsonRequestExecutionManualParams
        {
            public string UserName;
            public string FillPrice;
            public string FillClOrdId;
            public string FillSymbol;
            public string LogWho;
            public int FillQty;
            public int FillSideSell;
            public int BypassRisk;
            public int DoNothing;
            public int SendRealOrder;
            public int RealThruTicks;
        }

        public class jsonRequestLogin
        {
            public string UserName;
            public string Account;
            public string FDID;
        }

        public class jsonRequestParamsBasket
        {
            public string ParamBasketName;
            public jsonRequestSignalController SignalController;
            public jsonRequestInitiatorController InitiatorController;
            public jsonRequestLiquidatorController LiquidatorController;
        }

        public class jsonRequestAbortTrade
        {
            public string UserName;
            public int AbortWhat;  // 0-none, 7-all, 1-initiators, 2-liquidators, 4-signals
            public List<string> Symbols; // if null, abort all
        }

        public class jsonRequestInitiatorController
        {
            public jsonRequestExecutionHunterParams Hunter;
            public jsonRequestExecutionDrifterParams Drifter;
            public jsonRequestExecutionTrailerParams Trailer;
            public jsonRequestExecutionManualParams Manual;
        }

        public class jsonRequestLiquidatorController
        {
            public jsonRequestExecutionChaserParams Chaser;
            public jsonRequestExecutionBracketParams Bracket;
            public jsonRequestExecutionTrailerParams Trailer;
            public jsonRequestExecutionHunterParams Hunter;
            public jsonRequestExecutionDrifterParams Drifter;
            public jsonRequestExecutionManualParams Manual;
        }

        public class jsonRequestSignalController
        {
            public jsonRequestSignalTradeWatcherParams TradeWatcher;
        }

        public class jsonRequest
        {
            public jsonRequestParamsBasket ParamBasket;
            public jsonRequestParamTrades ParamTrades;
            public jsonRequestRunTrade RunTrade;
            public jsonRequestAbortTrade AbortTrade;
            public jsonRequestLogin Login;
            public jsonRequestRiskParams RiskParams;
            public jsonRequestParamWatchlist ParamWatchlist;
            public jsonRequestExecutionManualParams ManualAdjust;
        }

        public class jsonRequestSignalTradeWatcherParams
        {
            public string TTL;
            public int StaleTradeMs;
            public int StaleTheoMs;
            public int MaxNumSignals;
            public int MaxNumLoopsOnProfit;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceA;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceB;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceC;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceD;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceE;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceF;
            public jsonRequestSignalTradeWatcherInstanceParams InstanceG;
        }

        public class jsonRequestSignalTradeWatcherInstanceParams
        {
            public int Enabled;
            public ulong HashCode;
            public string InstanceName;

            public jsonRequestSignalTradeWatcherInstanceItemParams Item;
        }

        public class jsonRequestOptionFilterParams
        {
            public int MinDelta;
            public int MaxDelta;
            public int MinExpiry;
            public int MaxExpiry;
            public int MinExpiryDate;
            public int MaxExpiryDate;
            public string CallPuts;
        }

        public class jsonRequestSignalTradeWatcherInstanceItemParams
        {
            public int OrderQty;
            public int PctBid;
            public int UseAdjTheo;
            public string EdgeToTheo;
            public int ExcludeOnLoss;
            public int ExcludeOnScratch;
            public string AfterLosing0;
            public string AfterLosing1;
            public string BanFor0;
            public string BanFor1;
            public jsonRequestLeanBaseParams Lean;
            public jsonRequestOptionFilterParams OptionFilter;
            // these are only used internally for now, not on the backend
        }

        public class jsonRequestExecutionDrifterEnterParams
        {
            public int TTLHunt;
            public int PayupTicks;
            public jsonRequestLeanBaseParams Lean;
        }

        public class jsonRequestExecutionDrifterWorkCommonParams
        {
            public int TTLDrift;
            public string MinNbboSpreadPrice;
            public string MaxNbboSpreadPrice;
            public int MaxOrderCount;
            public int PukeOnExit;
            public int ThruTicks;
        }

        public class jsonRequestExecutionDrifterWorkAloneParams
        {
            public string MaxSideSpreadPrice;
            public int MinL2LeanQty;
            public int MinL2LeanCnt;
            public int TTLWaitForJoiners;
        }

        public class jsonRequestExecutionDrifterWorkOrphanedParams
        {
            public string MaxSideSpreadPrice;
            public int MinL2LeanQty;
            public int MinL2LeanCnt;
            public int TTLWaitForJoiners;
        }

        public class jsonRequestExecutionDrifterWorkImproveParams
        {
            public string MaxSideSpreadPrice;
            public int MinL1LeanQty;
            public int MinL1LeanCnt;
            public int MinL2LeanQty;
            public int MinL2LeanCnt;
        }

        public class jsonRequestExecutionDrifterWorkJoinedParams
        {
            public string MaxSideSpreadPrice;
            public int MinL1LeanQty;
            public int MinL1LeanCnt;
            public int MinL1LargeQty;
            public int MinL1LargeCnt;
            public int MinL2LeanQty;
            public int MinL2LeanCnt;
            public int TTLWaitForJoiners;
        }

        public class jsonRequestExecutionDrifterParams
        {
            public jsonRequestExecutionDrifterEnterParams Enter;
            public jsonRequestExecutionDrifterWorkCommonParams WorkCommon;
            public jsonRequestExecutionDrifterWorkAloneParams WorkAlone;
            public jsonRequestExecutionDrifterWorkJoinedParams WorkJoined;
            public jsonRequestExecutionDrifterWorkOrphanedParams WorkOrphaned;
            public jsonRequestExecutionDrifterWorkImproveParams WorkImprove;
        }
    }
}

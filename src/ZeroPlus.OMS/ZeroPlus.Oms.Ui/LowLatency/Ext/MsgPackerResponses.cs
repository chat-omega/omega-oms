using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.LowLatency.Ext
{
    public class MsgPackerResponses
    {
        public class jsonResponseBlockTimestamps
        {
            public string Nbbo { get; set; }
            public string Rbbo { get; set; }
            public string LastTrade { get; set; }
        }

        public class jsonResponsePnlBuySell
        {
            public string Buy;
            public string Sell;
        }

        public class jsonResponsePnlSymPos
        {
            public string Symbol;
            public jsonResponsePnlBuySell TotWrkQty;
            public jsonResponsePnlBuySell TotInfQty;
            public jsonResponsePnlBuySell TotPosQty;
            public jsonResponsePnlBuySell TotPosCost;
            public string OpenPos;
            public string OpenPosCost;
            public string PnlSameQty;
            public string Pnl;
        }

        public class jsonResponseOrder
        {
            // renumber
            public string Symbol;
            public string UserName; // BUGBUG
            public string UserId; // BUGBUG
            public string StratType;
            public string StratName;
            public string StratId;
            public string StratIdInResponseTo;
            public string SignalInstance;
            public string ClOrdId;
            public string Action;
            public string OrderPrice;
            public string RemOrderQty;
            public string FillPrice;
            public string FillQty;
            public string DiffMillis;
            public string ExecutedExchange;
            public jsonResponseBlockTimestamps BlockTimestamps;
            public string Error;
            public string ResponseToPrice;
            public string ResponseToClOrdId;
            public string OrderExtra;
        }

        public class jsonResponseNbboItem
        {
            public string Symbol;
            public string Bid;
            public string Ask;
            public string NbboT;
            public string RbboT;
            public string LastTradeT;
        }

        public class jsonResponseUpdate
        {
            // renumber
            public string Action;
            public string UserName; // BUGBUG
            public string Error; // BUGBUG
        }

        public class jsonResponse
        {
            public string Timestamp;
            public string AppProcess;
            public string AppThread;
            public jsonResponseOrder Order;
            public jsonResponseUpdate Update;
            public List<jsonResponseNbboItem> Nbbo;
            public string Error;
            public msgResponseStratStats StratStats;

            [JsonIgnore]
            public LowLatencyInstance LowLatencyInstance { get; set; }
        }

        public class msgResponse
        {
            public string Timestamp;
            public string Symbol;
            public string StratType;
            public string StratName;
            public string ClOrdId;
            public string Action;
            public string OrderPrice;
            public string RemOrderQty;
            public string FillPrice;
            public string FillQty;
            public string Nbbo;
            public string ExecutedExchange;
            public jsonResponseBlockTimestamps BlockTimestamps;
        }

        // ,"StratStats":{"Total":"2","Execution":{"H":"1"},"Signal":{"w":"1"}},

        //[MessagePackObject]
        public class msgResponseStratStats
        {
            public string Total;
            public Dictionary<string, string> Initiators;
            public Dictionary<string, string> Liquidators;
            public Dictionary<string, string> Signals;
        }
    }
}

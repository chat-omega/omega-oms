using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Trading
{
    public class OpraDatabaseTradesRequest
    {
        public OpraDatabaseTradesRequest(int requestId, List<string> underlyingSymbols, List<string> symbols, bool requestSpreads, bool realTime, DateTime startTime, DateTime endTime, string constraint1, string constraint2, int deltaAdjEdgeInterval, bool stop, bool matchIoiTrades)
        {
            RequestId = requestId;
            UnderlyingSymbols = underlyingSymbols;
            Symbols = symbols;
            RequestSpreads = requestSpreads;
            RealTime = realTime;
            StartTime = startTime;
            EndTime = endTime;
            Constraint1 = constraint1;
            Constraint2 = constraint2;
            DeltaAdjEdgeInterval = deltaAdjEdgeInterval;
            Stop = stop;
            MatchIoiTrades = matchIoiTrades;
        }

        public OpraDatabaseTradesRequest() { }

        public int RequestId { get; }
        public List<string> UnderlyingSymbols { get; } = [];
        public List<string> Symbols { get; } = [];
        public bool RequestSpreads { get; }
        public bool RealTime { get; }
        public DateTime StartTime { get; }
        public DateTime EndTime { get; }
        public string Constraint1 { get; } = string.Empty;
        public string Constraint2 { get; } = string.Empty;
        public int DeltaAdjEdgeInterval { get; } = 60;
        public bool Stop { get; }
        public bool MatchIoiTrades { get; set; }
    }
}

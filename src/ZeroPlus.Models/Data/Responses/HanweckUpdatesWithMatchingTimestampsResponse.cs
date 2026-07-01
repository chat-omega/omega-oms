using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Responses
{
    public class HanweckUpdatesWithMatchingTimestampsResponse
    {
        public readonly int RequestId;
        public readonly bool UpdateFound;
        public readonly DateTime Timestamp;
        public readonly double Price;
        public readonly Dictionary<string, double> SymbolToTheoMap;

        public HanweckUpdatesWithMatchingTimestampsResponse(int requestId)
        {
            RequestId = requestId;
            UpdateFound = false;
            Timestamp = default;
            Price = double.NaN;
            SymbolToTheoMap = new Dictionary<string, double>();
        }

        public HanweckUpdatesWithMatchingTimestampsResponse(int requestId, bool updateFound, DateTime timestamp, double price, Dictionary<string, double> symbolToTheoMap)
        {
            RequestId = requestId;
            UpdateFound = updateFound;
            Timestamp = timestamp;
            Price = price;
            SymbolToTheoMap = symbolToTheoMap;
        }
    }
}

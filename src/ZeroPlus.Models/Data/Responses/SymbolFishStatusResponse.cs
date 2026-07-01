using System;

namespace ZeroPlus.Models.Data.Responses
{
    public class SymbolFishStatusResponse
    {
        public Generated.FishStatus FishStatus;
        public double FishLevel;
        public double FishEdge;
        public double FishLevelSell;
        public double FishEdgeSell;
        public DateTime LastFishTime;
        public string Symbol;
        public static SymbolFishStatusResponse New = new SymbolFishStatusResponse("", Generated.FishStatus.New, 0, 0, 0, 0, default);

        public SymbolFishStatusResponse(string symbol, Generated.FishStatus fishStatus, double fishLevel, double fishEdge, double fishLevelSell, double fishEdgeSell, DateTime lastFishTime)
        {
            Symbol = symbol;
            FishStatus = fishStatus;
            FishLevel = fishLevel;
            FishEdge = fishEdge;
            FishLevelSell = fishLevelSell;
            FishEdgeSell = fishEdgeSell;
            LastFishTime = lastFishTime;
        }
    }
}
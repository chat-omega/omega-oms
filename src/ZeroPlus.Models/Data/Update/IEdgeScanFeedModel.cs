using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public interface IEdgeScanFeedModel
    {
        EdgeScannerType EdgeScannerType { get; set; }

        string SessionId { get; set; }

        bool IsFirm { get; set; }
        bool PossibleFirm { get; set; }
        bool PossibleCopyCat { get; set; }
        bool Uncertain { get; set; }
        double ReceiveLatency { get; set; }

        string UnderSymbol { get; set; }
        string SpreadId { get; set; }
        string SpreadType { get; set; }
        string Description { get; set; }
        byte LegsCount { get; set; }
        DateTime NearExpiration { get; set; }
        DateTime FarExpiration { get; set; }
        double SpreadWidth { get; set; }
        double HighestLegDelta { get; set; }
        double IvPctChange { get; set; }
        double SpreadWeightedVega { get; set; }

        string BuySymbol { get; set; }
        DateTime BuyTime { get; set; }
        ushort BuyQty { get; set; }
        double BuyPrice { get; set; }
        double BuyTradeOriginalPrice { get; set; }
        ushort BuyBidSize { get; set; }
        ushort BuyAskSize { get; set; }
        double BuyEdgeToTheo { get; set; }
        double BuyVolaEdgeToTheo { get; set; }
        double BuyMinEdgeToTheo { get; }
        double BuyTradeUnderlyingMid { get; set; }
        double BuyTradeBid { get; set; }
        double BuyTradeMid { get; set; }
        double BuyTradeAsk { get; set; }
        double BuyTradeTheo { get; set; }
        double BuyTradeDelta { get; set; }
        double BuyUnderlyingWidth { get; set; }
        char BuyConditionCode { get; set; }

        string SellSymbol { get; set; }
        DateTime SellTime { get; set; }
        ushort SellQty { get; set; }
        double SellPrice { get; set; }
        double SellTradeOriginalPrice { get; set; }
        ushort SellBidSize { get; set; }
        ushort SellAskSize { get; set; }
        double SellEdgeToTheo { get; set; }
        double SellVolaEdgeToTheo { get; set; }
        double SellMinEdgeToTheo { get; }
        double SellTradeUnderlyingMid { get; set; }
        double SellTradeBid { get; set; }
        double SellTradeMid { get; set; }
        double SellTradeAsk { get; set; }
        double SellTradeTheo { get; set; }
        double SellTradeDelta { get; set; }
        double SellUnderlyingWidth { get; set; }
        char SellConditionCode { get; set; }

        double DeltaAdjEdge { get; set; }
        string Exchange { get; set; }
        double AdjustedPnl { get; set; }
        double Position { get; set; }
        double Ttl { get; set; }
        ushort FlipCount { get; set; }
        bool QtyMismatch { get; set; }

        Side AdjSide { get; set; }
        Side IbCobSide { get; set; }
        double IbCobBid { get; set; }
        double IbCobAsk { get; set; }
        string ExtraTag { get; set; }

        string Message { get; set; }
        string Reason { get; set; }
    }
}
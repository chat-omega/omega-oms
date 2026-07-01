using System;

namespace ZeroPlus.Models.Data.Update
{
    public readonly struct DeltaAdjustedTheoDetailsModel
    {
        public readonly double Delta;
        public readonly double Theo;
        public readonly double MidPrice;
        public readonly double DeltaAdjustedTheo;
        public readonly double BidUpdate;
        public readonly double AskUpdate;
        public readonly DateTime SnapShotTime;
        public readonly string Symbol;

        public DeltaAdjustedTheoDetailsModel(double delta, double theo, double midPrice, double deltaAdjustedTheo, double bidUpdate, double askUpdate, DateTime snapshotTime, string symbol)
        {
            Delta = delta;
            Theo = theo;
            MidPrice = midPrice;
            DeltaAdjustedTheo = deltaAdjustedTheo;
            BidUpdate = bidUpdate;
            AskUpdate = askUpdate;
            SnapShotTime = snapshotTime;
            Symbol = symbol;
        }
    }
}
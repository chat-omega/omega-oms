namespace ZeroPlus.Models.Data.Models
{
    public readonly struct AdjTheoUpdate
    {
        public readonly int TickerId;
        public readonly uint Sequence;
        public readonly double Theo;
        public readonly double SmoothTheo;
        public readonly double Underlying;
        public readonly bool JumpDetected;
        public readonly double SecondaryTheo;
        public readonly double SecondaryTheoAdj;
        public readonly double PriceMetric;
        public readonly byte ModelId;
        public readonly double SecondaryVol;
        public readonly double ChangeInPremium;
        public readonly double SecondarySpot;
        public readonly double DaEma;
        public readonly double VolaEma;

        public AdjTheoUpdate(
            int tickerId,
            uint sequence,
            double theo,
            double smoothTheo,
            double underlying,
            bool jumpDetected,
            double secondaryTheo,
            double secondaryTheoAdj,
            double priceMetric,
            byte model,
            double secondaryVol,
            double changeInPremium,
            double secondarySpot,
            double daEma,
            double volaEma)
        {
            TickerId = tickerId;
            Sequence = sequence;
            Theo = theo;
            SmoothTheo = smoothTheo;
            Underlying = underlying;
            JumpDetected = jumpDetected;
            SecondaryTheo = secondaryTheo;
            SecondaryTheoAdj = secondaryTheoAdj;
            PriceMetric = priceMetric;
            ModelId = model;
            SecondaryVol = secondaryVol;
            ChangeInPremium = changeInPremium;
            SecondarySpot = secondarySpot;
            DaEma = daEma;
            VolaEma = volaEma;
        }
    }
}

namespace ZeroPlus.Models.Data.Update
{
    public class EmaUpdateModel
    {
        public ulong Sequence = 0;

        public double LowPeriodEma = double.NaN;
        public double LowPeriodEmaAdj = double.NaN;
        public double LowPeriodEmaUnderlying = double.NaN;

        public double MidPeriodEma = double.NaN;
        public double MidPeriodEmaAdj = double.NaN;
        public double MidPeriodEmaUnderlying = double.NaN;

        public double HighPeriodEma = double.NaN;
        public double HighPeriodEmaAdj = double.NaN;
        public double HighPeriodEmaUnderlying = double.NaN;

        public double MidPeriodBidEma = double.NaN;
        public double MidPeriodBidEmaAdj = double.NaN;

        public double MidPeriodAskEma = double.NaN;
        public double MidPeriodAskEmaAdj = double.NaN;

        // Timestamps (nanoseconds since Unix epoch)
        public ulong QuoteTimestampNanos = 0;
        public ulong CalculationTimestampNanos = 0;
        public ulong LowPeriodEmaTimestampNanos = 0;
        public ulong MidPeriodEmaTimestampNanos = 0;
        public ulong HighPeriodEmaTimestampNanos = 0;

        public EmaUpdateModel() { }

        public EmaUpdateModel(ulong sequence,
                              double lowPeriodEma,
                              double lowPeriodEmaAdj,
                              double lowPeriodEmaUnderlying,
                              double midPeriodEma,
                              double midPeriodEmaAdj,
                              double midPeriodEmaUnderlying,
                              double highPeriodEma,
                              double highPeriodEmaAdj,
                              double highPeriodEmaUnderlying,
                              double midPeriodBidEma,
                              double midPeriodBidEmaAdj,
                              double midPeriodAskEma,
                              double midPeriodAskEmaAdj,
                              ulong quoteTimestampNanos = 0,
                              ulong calculationTimestampNanos = 0,
                              ulong lowPeriodEmaTimestampNanos = 0,
                              ulong midPeriodEmaTimestampNanos = 0,
                              ulong highPeriodEmaTimestampNanos = 0)
        {
            Sequence = sequence;

            LowPeriodEma = lowPeriodEma;
            LowPeriodEmaAdj = lowPeriodEmaAdj;
            LowPeriodEmaUnderlying = lowPeriodEmaUnderlying;

            MidPeriodEma = midPeriodEma;
            MidPeriodEmaAdj = midPeriodEmaAdj;
            MidPeriodEmaUnderlying = midPeriodEmaUnderlying;

            MidPeriodBidEma = midPeriodBidEma;
            MidPeriodBidEmaAdj = midPeriodBidEmaAdj;
            MidPeriodAskEma = midPeriodAskEma;
            MidPeriodAskEmaAdj = midPeriodAskEmaAdj;

            HighPeriodEma = highPeriodEma;
            HighPeriodEmaAdj = highPeriodEmaAdj;
            HighPeriodEmaUnderlying = highPeriodEmaUnderlying;

            QuoteTimestampNanos = quoteTimestampNanos;
            CalculationTimestampNanos = calculationTimestampNanos;
            LowPeriodEmaTimestampNanos = lowPeriodEmaTimestampNanos;
            MidPeriodEmaTimestampNanos = midPeriodEmaTimestampNanos;
            HighPeriodEmaTimestampNanos = highPeriodEmaTimestampNanos;
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace ZeroPlus.Models.Data.SharedMemoryModels
{
    /// <summary>Explicit layout for Native AOT interop (<c>SharedArray&lt;T&gt;</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DeltaAdjustedEmaModel
    {
        public const int SYMBOL_LEN = 30;

        public fixed byte SymbolBytes[SYMBOL_LEN];
        public double LowPeriodEma;
        public double LowPeriodEmaAdj;
        public double MidPeriodBidEma;
        public double MidPeriodBidEmaAdj;
        public double MidPeriodEma;
        public double MidPeriodEmaAdj;
        public double MidPeriodAskEma;
        public double MidPeriodAskEmaAdj;
        public double HighPeriodEma;
        public double HighPeriodEmaAdj;

        // Timestamps (nanoseconds since Unix epoch)
        public ulong QuoteTimestampNanos;           // When quote was received from market data
        public ulong CalculationTimestampNanos;     // When delta adjustment was calculated
        public ulong LowPeriodEmaTimestampNanos;    // When low period EMA was last updated
        public ulong MidPeriodEmaTimestampNanos;    // When mid period EMA was last updated
        public ulong HighPeriodEmaTimestampNanos;   // When high period EMA was last updated

        public string Symbol
        {
            set
            {
                int len = value.Length < SYMBOL_LEN ? value.Length : SYMBOL_LEN;
                for (int i = 0; i < len; i++)
                {
                    SymbolBytes[i] = (byte)value[i];
                }
            }

            get
            {
                Span<char> buffer = stackalloc char[SYMBOL_LEN];
                int len = 0;
                for (; len < SYMBOL_LEN; len++)
                {
                    if (SymbolBytes[len] == 0)
                        break;
                    buffer[len] = (char)SymbolBytes[len];
                }
                return new string(buffer[..len]);
            }
        }
    }
}

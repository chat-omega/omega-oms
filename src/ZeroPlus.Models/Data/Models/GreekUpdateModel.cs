using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Data.Models
{
    public class GreekUpdateModel : IGreekUpdate
    {
        const int THEO_VOL_BIT_MASK = 31;

        public readonly Security? Security;

        public object Lock { get; } = new();

        public int Index { get; set; }

        public int BidSize { get; set; }
        public int AskSize { get; set; }

        public byte BidMCID { get; set; }
        public byte AskMCID { get; set; }

        public uint InfoBits { get; set; }

        public double BidPrice { get; set; }
        public double AskPrice { get; set; }
        public double Theo { get; set; }
        public double ImpliedVolatility { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double Rho { get; set; }
        public double BidVol { get; set; }
        public double AskVol { get; set; }
        public double MidVol { get; set; }

        public double UBidPrice { get; set; }
        public double UAskPrice { get; set; }

        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FvDivs { get; set; }

        public ulong SequenceNumber { get; set; }
        public ulong TradeVolume { get; set; }

        public ulong TimeStamp { get; set; }
        public ulong CollectorTimestamp { get; set; }
        public ulong CollectorTimestampNanos { get; set; }
        public ulong CalculationTimestampNanos { get; set; }
        public ulong BidTimestampNanos { get; set; }
        public ulong AskTimestampNanos { get; set; }

        public ulong UTimestampNanos { get; set; }
        public ulong PersistorTimestampNanos { get; set; }
        public ulong PersistorSeqNum { get; set; }

        public GreekUpdateModel()
        {

        }

        public GreekUpdateModel(Security security)
        {
            Security = security;
        }

        public GreekUpdateModel(Security security,
            double delta,
            double gamma,
            double vega,
            double theta,
            double rho,
            double impliedVolatility,
            double theo,
            ulong timestamp,
            ulong collectorTimestamp,
            uint infoBits = 0)
        {
            Security = security;
            Delta = delta;
            Gamma = gamma;
            Vega = vega;
            Theta = theta;
            Rho = rho;
            ImpliedVolatility = impliedVolatility;
            Theo = theo;
            TimeStamp = timestamp;
            CollectorTimestamp = collectorTimestamp;
            InfoBits = infoBits;
        }

        public override string ToString()
        {
            return "Security: " + Security + ", " +
                   "Index: " + Index + ", " +
                   "Delta: " + Delta + ", " +
                   "Gamma: " + Gamma + ", " +
                   "Vega: " + Vega + ", " +
                   "Theta: " + Theta + ", " +
                   "Rho: " + Rho + ", " +
                   "ImpliedVolatility: " + ImpliedVolatility + ", " +
                   "Theo: " + Theo + ", " +
                   "Info Bits: " + InfoBits + ", " +
                   "TimeStamp: " + TimeStamp.ToHHMMSSffffff() + ", " +
                   "CollectorTimestamp: " + CollectorTimestamp.ToHHMMSSffffff();
        }
    }
}

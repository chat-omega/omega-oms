using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ZeroPlus.Oms.Clients
{
    public class DeltaAdjTheo
    {
        public string Option { get; }
        public uint UpdateSequence { get; }
        public double DeltaAdjustedTheo { get; }
        public double SmoothedDeltaAdjustedTheo { get; }
        public double Underlying { get; }
        public double SecondaryTheo { get; }
        public double SecondaryTheoAdj { get; }
        public double PriceMetric { get; }
        public bool JumpDetected { get; }
        public byte ModelId { get; }
        public double SecondaryVol { get; }
        public double ChangeInPremium { get; }
        public double SecondarySpot { get; }
        public double AdjDaEma { get; }
        public double AdjVolaEma { get; }

        public DeltaAdjTheo(string option,
            uint updateSequence,
            double deltaAdjustedTheo,
            double smoothedTheo,
            double underlying,
            bool jumpDetected,
            double secondaryTheo = double.NaN,
            double secondaryTheoAdj = double.NaN,
            double priceMetric = double.NaN,
            byte modelId = 0,
            double secondaryVol = double.NaN,
            double changeInPremium = double.NaN,
            double secondarySpot = double.NaN,
            double daEma = double.NaN,
            double volaEma = double.NaN)
        {
            Option = option;
            UpdateSequence = updateSequence;
            DeltaAdjustedTheo = deltaAdjustedTheo;
            SmoothedDeltaAdjustedTheo = smoothedTheo;
            Underlying = underlying;
            JumpDetected = jumpDetected;
            SecondaryTheo = secondaryTheo;
            SecondaryTheoAdj = secondaryTheoAdj;
            PriceMetric = priceMetric;
            ModelId = modelId;
            SecondaryVol = secondaryVol;
            ChangeInPremium = changeInPremium;
            SecondarySpot = secondarySpot;
            AdjDaEma = daEma;
            AdjVolaEma = volaEma;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UpdateSequence, DeltaAdjustedTheo);
        }

        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is DeltaAdjTheo other)
            {
                return Option == other.Option && UpdateSequence == other.UpdateSequence;
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append(Option);
            stringBuilder.Append(',');
            stringBuilder.Append(UpdateSequence);
            stringBuilder.Append(',');
            stringBuilder.Append(SmoothedDeltaAdjustedTheo);
            stringBuilder.Append(',');
            stringBuilder.Append(DeltaAdjustedTheo);
            stringBuilder.Append(',');
            stringBuilder.Append(Underlying);
            return stringBuilder.ToString();
        }
    }
}
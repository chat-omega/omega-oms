using System;

namespace ZeroPlus.Oms.Ui.Models.Volatility
{
    public enum PositionSide
    {
        Long,
        Short
    }

    public class OptionLeg
    {
        public OptionType Type { get; set; }
        public PositionSide Side { get; set; }
        public double Strike { get; set; }
        public double TimeToExpiry { get; set; } // Years

        public double Multiplier => Side == PositionSide.Long ? 1.0 : -1.0;
    }

    public class SpreadStrategy
    {
        public double ReferencePrice { get; set; }
        public double ImpliedVol { get; set; }
        public double RiskFreeRate { get; set; }
        public double BorrowRate { get; set; }

        public OptionLeg Leg1 { get; set; } = new OptionLeg();
        public OptionLeg Leg2 { get; set; } = new OptionLeg();

        public double GetPayoffAt(double underlyingPrice)
        {
            double p1 = BlackScholesCalculator.Price(Leg1.Type, underlyingPrice, Leg1.Strike, RiskFreeRate, ImpliedVol, Leg1.TimeToExpiry, BorrowRate);
            double p2 = BlackScholesCalculator.Price(Leg2.Type, underlyingPrice, Leg2.Strike, RiskFreeRate, ImpliedVol, Leg2.TimeToExpiry, BorrowRate);

            // Initial cost is theoretical price at ReferencePrice
            double c1 = BlackScholesCalculator.Price(Leg1.Type, ReferencePrice, Leg1.Strike, RiskFreeRate, ImpliedVol, Leg1.TimeToExpiry, BorrowRate);
            double c2 = BlackScholesCalculator.Price(Leg2.Type, ReferencePrice, Leg2.Strike, RiskFreeRate, ImpliedVol, Leg2.TimeToExpiry, BorrowRate);

            double profit1 = (p1 - c1) * Leg1.Multiplier;
            double profit2 = (p2 - c2) * Leg2.Multiplier;

            return profit1 + profit2;
        }

        public Greeks GetNetGreeksAt(double underlyingPrice)
        {
            var g1 = GetGreeksForLeg(Leg1, underlyingPrice);
            var g2 = GetGreeksForLeg(Leg2, underlyingPrice);

            return new Greeks
            {
                Delta = g1.Delta + g2.Delta,
                Gamma = g1.Gamma + g2.Gamma,
                Theta = g1.Theta + g2.Theta,
                Vega = g1.Vega + g2.Vega
            };
        }

        private Greeks GetGreeksForLeg(OptionLeg leg, double underlyingPrice)
        {
            double m = leg.Multiplier;
            return new Greeks
            {
                Delta = BlackScholesCalculator.Delta(leg.Type, underlyingPrice, leg.Strike, RiskFreeRate, ImpliedVol, leg.TimeToExpiry, BorrowRate) * m,
                Gamma = BlackScholesCalculator.Gamma(underlyingPrice, leg.Strike, RiskFreeRate, ImpliedVol, leg.TimeToExpiry, BorrowRate) * m,
                Theta = BlackScholesCalculator.Theta(leg.Type, underlyingPrice, leg.Strike, RiskFreeRate, ImpliedVol, leg.TimeToExpiry, BorrowRate) * m,
                Vega = BlackScholesCalculator.Vega(underlyingPrice, leg.Strike, RiskFreeRate, ImpliedVol, leg.TimeToExpiry, BorrowRate) * m
            };
        }
    }

    public struct Greeks
    {
        public double Delta;
        public double Gamma;
        public double Theta;
        public double Vega;
    }
}

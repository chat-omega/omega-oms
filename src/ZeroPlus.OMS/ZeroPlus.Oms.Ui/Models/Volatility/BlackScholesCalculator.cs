using MathNet.Numerics.Distributions;
using System;

namespace ZeroPlus.Oms.Ui.Models.Volatility
{
    public enum OptionType
    {
        Call,
        Put
    }

    public static class BlackScholesCalculator
    {
        private static readonly Normal _normal = new Normal(0, 1);

        public static double Price(OptionType type, double S, double K, double r, double sigma, double T, double q = 0)
        {
            if (T <= 0) return Math.Max(0, type == OptionType.Call ? S - K : K - S);

            double d1 = CalculateD1(S, K, r, q, sigma, T);
            double d2 = CalculateD2(d1, sigma, T);

            if (type == OptionType.Call)
            {
                return S * Math.Exp(-q * T) * _normal.CumulativeDistribution(d1) - K * Math.Exp(-r * T) * _normal.CumulativeDistribution(d2);
            }
            else
            {
                return K * Math.Exp(-r * T) * _normal.CumulativeDistribution(-d2) - S * Math.Exp(-q * T) * _normal.CumulativeDistribution(-d1);
            }
        }

        public static double Delta(OptionType type, double S, double K, double r, double sigma, double T, double q = 0)
        {
            if (T <= 0) return 0; // Simplified for expiration

            double d1 = CalculateD1(S, K, r, q, sigma, T);
            return type == OptionType.Call ? Math.Exp(-q * T) * _normal.CumulativeDistribution(d1) : Math.Exp(-q * T) * (_normal.CumulativeDistribution(d1) - 1);
        }

        public static double Gamma(double S, double K, double r, double sigma, double T, double q = 0)
        {
            if (T <= 0) return 0;

            double d1 = CalculateD1(S, K, r, q, sigma, T);
            return (Math.Exp(-q * T) * _normal.Density(d1)) / (S * sigma * Math.Sqrt(T));
        }

        public static double Vega(double S, double K, double r, double sigma, double T, double q = 0)
        {
            if (T <= 0) return 0;

            double d1 = CalculateD1(S, K, r, q, sigma, T);
            // Vega is standardly S * exp(-qT) * N'(d1) * sqrt(T)
            return S * Math.Exp(-q * T) * _normal.Density(d1) * Math.Sqrt(T) / 100.0;
        }

        public static double Theta(OptionType type, double S, double K, double r, double sigma, double T, double q = 0)
        {
            if (T <= 0) return 0;

            double d1 = CalculateD1(S, K, r, q, sigma, T);
            double d2 = CalculateD2(d1, sigma, T);

            double term1 = -(S * Math.Exp(-q * T) * _normal.Density(d1) * sigma) / (2 * Math.Sqrt(T));
            double qTerm = q * S * Math.Exp(-q * T);
            double rTerm = r * K * Math.Exp(-r * T);

            if (type == OptionType.Call)
            {
                return (term1 - rTerm * _normal.CumulativeDistribution(d2) + qTerm * _normal.CumulativeDistribution(d1)) / 365.0;
            }
            else
            {
                return (term1 + rTerm * _normal.CumulativeDistribution(-d2) - qTerm * _normal.CumulativeDistribution(-d1)) / 365.0;
            }
        }

        private static double CalculateD1(double S, double K, double r, double q, double sigma, double T)
        {
            return (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
        }

        private static double CalculateD2(double d1, double sigma, double T)
        {
            return d1 - sigma * Math.Sqrt(T);
        }
    }
}

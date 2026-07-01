using MathNet.Numerics.Distributions;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public interface IOptionPricingService
    {
        double CalculateOptionPrice(OptionParameters parameters);
        double CalculatePnL(PnLParameters parameters);
    }

    public record OptionParameters(
        double SpotPrice,
        double Strike,
        double TimeToExpiry,
        double RiskFreeRate,
        double Volatility,
        bool IsCall
    );

    public record PnLParameters(
        OptionParameters CurrentParams,
        OptionParameters InitialParams,
        int Quantity
    );

    public class BlackScholesMertonService : IOptionPricingService
    {
        /// <summary>
        /// Calculates option price using the Black-Scholes-Merton model.
        /// </summary>
        /// <param name="parameters">The option parameters containing:
        ///   <list type="bullet">
        ///     <item><term>SpotPrice (S)</term><description>Current price of the underlying asset</description></item>
        ///     <item><term>Strike (K)</term><description>Strike price of the option</description></item>
        ///     <item><term>TimeToExpiry (T)</term><description>Time to expiration in years</description></item>
        ///     <item><term>RiskFreeRate (r)</term><description>Risk-free interest rate (decimal, not percentage)</description></item>
        ///     <item><term>Volatility (sigma)</term><description>Implied volatility of the underlying asset (decimal, not percentage)</description></item>
        ///     <item><term>IsCall</term><description>True for a call option, false for a put option</description></item>
        ///   </list>
        /// </param>
        /// <returns>The theoretical price of the option according to Black-Scholes-Merton</returns>
        /// <exception cref="ArgumentException">Thrown when any numeric parameter is invalid (zero or negative)</exception>
        /// <remarks>
        /// Uses the standard BSM formula:
        /// <para>Call = S * N(d1) - K * e^(-rT) * N(d2)</para>
        /// <para>Put = K * e^(-rT) * N(-d2) - S * N(-d1)</para>
        /// 
        /// Where:
        /// <para>d1 = (ln(S/K) + (r + σ²/2)T) / (σ√T)</para>
        /// <para>d2 = d1 - σ√T</para>
        /// </remarks>
        public double CalculateOptionPrice(OptionParameters parameters)
        {
            var (S, K, T, r, sigma, isCall) = parameters;
            double d1 = (Math.Log(S / K) + (r + sigma * sigma / 2) * T) / (sigma * Math.Sqrt(T));
            double d2 = d1 - sigma * Math.Sqrt(T);
            Normal normal = new(0, 1);

            if (isCall)
            {
                return S * normal.CumulativeDistribution(d1) -
                       K * Math.Exp(-r * T) * normal.CumulativeDistribution(d2);
            }

            return K * Math.Exp(-r * T) * normal.CumulativeDistribution(-d2) -
                   S * normal.CumulativeDistribution(-d1);
        }

        public double CalculatePnL(PnLParameters parameters)
        {
            double currentValue = CalculateOptionPrice(parameters.CurrentParams);
            double initialValue = CalculateOptionPrice(parameters.InitialParams);
            return (currentValue - initialValue) * parameters.Quantity;
        }
    }
}
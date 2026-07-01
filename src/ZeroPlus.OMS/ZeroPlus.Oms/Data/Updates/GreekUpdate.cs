using System;

namespace ZeroPlus.Oms.Data.Updates
{
    public class GreekUpdate
    {
        public double Delta { get; set; }
        public double Vega { get; set; }
        public double Theo { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Implied { get; set; }
        public double Rho { get; set; }
        public string HanweckTime { get; set; }
        public DateTime HanweckTimeRaw { get; set; }
        public int InfoBits { get; internal set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public override string ToString()
        {
            return $"Delta: {Delta}, " +
                   $"Vega: {Vega}, " +
                   $"Theo: {Theo}, " +
                   $"Gamma: {Gamma}, " +
                   $"Theta: {Theta}, " +
                   $"Implied: {Implied}, " +
                   $"Rho: {Rho}, " +
                   $"HanweckTime: {HanweckTime}, " +
                   $"InfoBits: {InfoBits}";
        }
    }
}

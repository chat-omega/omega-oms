using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Securities
{
    public class Security
    {
        public string Symbol { get; set; }
        public int ID { get; set; }
        public double MinimumTick { get; set; }
        public double Multiplier { get; set; }
        public SecurityType SecurityType { get; set; }
        public SecurityCategory SecurityCategory { get; set; }
        public string PrimaryExchange { get; set; }

        public Security()
        {
            Symbol = string.Empty;
            PrimaryExchange = string.Empty;
        }

        public static Security? GetUnderlying(Security security)
        {
            return security.SecurityType == SecurityType.Option ? ((Option)security).Underlying : security;
        }

        public override string ToString()
        {
            return "Symbol: " + Symbol;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 391 + Symbol.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj == this)
            {
                return true;
            }

            if (obj.GetType() != typeof(Security))
            {
                return false;
            }

            Security other = (Security)obj;

            return Symbol == other.Symbol && SecurityType == other.SecurityType;
        }
    }
}

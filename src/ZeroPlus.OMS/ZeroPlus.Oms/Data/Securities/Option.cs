using System;
using ZeroPlus.Comms.Models.Data.MarketData;

namespace ZeroPlus.Oms.Data.Securities
{
    [Serializable]
    public enum OptionType
    {
        PUT,
        CALL,
    }

    [Serializable]
    public enum SecurityType
    {
        Unknown,
        Stock,
        Option,
    }

    [Serializable]
    public class Option
    {
        private string _optionSymbol;

        public string UnderlyingSymbol { get; set; }
        public string RootSymbol { get; set; }
        public DateTime Expiration { get; set; }
        public double Strike { get; set; }
        public OptionType Type { get; set; }
        public SecurityType SecurityType { get; set; }
        public double Multiplier { get; set; }
        public MinimumTickStyle TickType { get; set; } = MinimumTickStyle.AllPenny;
        public double MinimumTick { get; set; } = 0.01;
        public string OptionSymbol
        {
            get => _optionSymbol;
            set
            {
                _optionSymbol = value;
                if (OptionSymbol == null)
                {
                    SecurityType = SecurityType.Unknown;
                }
                else if (OptionSymbol.StartsWith("."))
                {
                    SecurityType = SecurityType.Option;
                }
                else
                {
                    SecurityType = SecurityType.Stock;
                }
                Multiplier = SecurityType == SecurityType.Option ? 100 : 1;
            }
        }

        public override string ToString()
        {
            return OptionSymbol;
        }
    }
}

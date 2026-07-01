using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Securities
{
    public class Option : Security
    {
        public static readonly HashSet<string> Extra15minOptions = new HashSet<string>(new string[15]
        {
            "DJX",
            "DXL",
            "MUT",
            "VIX",
            "XEO",
            "OEX",
            "SPX",
            "XSP",
            "SPL",
            "SML",
            "MNX",
            "NDX",
            "RUT",
            "RUI",
            "RMN"
        });

        public static readonly HashSet<string> EthSymbols = new HashSet<string>(new string[5]
        {
            "SPX",
            "SPXW",
            "SPXQ",
            "SPXPM",
            "VIX"
        });

        public string? RootSymbol { get; set; }
        public DateTime Expiration { get; set; }
        public PutCall PutCall { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
        public double Strike { get; set; }
        public Security? Underlying { get; set; }

        static Option()
        {
        }

        public Option()
        {
            SecurityType = SecurityType.Option;
            MinimumTick = 0.01;
            Multiplier = 100.0;
        }

        public override string ToString()
        {
            return "Option Symbol:" + Symbol + ", Security Type:" + SecurityType + ", Expiration:" + Expiration + ", Strike:" + Strike + ", PutCall:" + PutCall;
        }
    }
}

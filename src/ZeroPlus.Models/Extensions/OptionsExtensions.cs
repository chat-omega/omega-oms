using System.Collections.Generic;

namespace ZeroPlus.Models.Extensions
{
    public static class OptionsExtensions
    {
        public static readonly HashSet<string> Indices = new()
        {
            "BKX", "DJX", "HGX", "MRUT", "MXEA", "MXEF", "NANOS", "NDX", "NQX", "NYFANG", "OEX", "OSX", "RLG", "RLV",
            "RUI", "RUT", "SIXB", "SIXI", "SIXM", "SIXRE", "SIXU", "SIXV", "SOX", "SPESG", "SPIKE", "SPX",
            "UTY", "VIX", "XAU", "XDA", "XDB", "XDC", "XDE", "XDN", "XDS", "XDZ", "XEO", "XND", "XSP"
        };

        public static bool IsIndex(this string value)
        {
            return !string.IsNullOrEmpty(value) && Indices.Contains(value.ToUpper());
        }
    }
}

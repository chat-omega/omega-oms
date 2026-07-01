using System.Collections.Generic;

namespace ZeroPlus.Oms.Helper;

public static class ExchangeHelper
{
    private static readonly Dictionary<char, string> OpraMicMap = new()
    {
        { 'A', "AMXO" }, { 'B', "XBOX" }, { 'C', "XCBO" }, { 'D', "EMLD" },
        { 'E', "EDGO" }, { 'H', "GMNI" }, { 'I', "XISX" }, { 'J', "MCRY" },
        { 'M', "XMIO" }, { 'N', "ARCO" }, { 'O', "OPRA" }, { 'P', "MPRL" },
        { 'Q', "XNDQ" }, { 'S', "SPHR" }, { 'T', "XBXO" }, { 'U', "MEMX" },
        { 'W', "CBSX" }, { 'X', "XPSX" }, { 'Y', "BATY" }, { 'Z', "BATS" },
    };

    public static string GetExchangeName(byte mcid)
    {
        char c = (char)mcid;
        return OpraMicMap.TryGetValue(c, out var name) ? name : c.ToString();
    }
}

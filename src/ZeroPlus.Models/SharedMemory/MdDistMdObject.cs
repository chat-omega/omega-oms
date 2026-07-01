using System.Runtime.InteropServices;

namespace ZeroPlus.Models.SharedMemory
{
    /// <summary>Explicit layout for Native AOT interop (<c>SharedArray&lt;T&gt;</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MdDistMdObject
    {
        public fixed byte symbol[24];
        public int BidQty;
        public int AskQty;
        public double BidPrice;
        public double AskPrice;
        public int LastTradeQty;
        public double LastTradePrice;
        public long quoteTime;
        public long tradeTime;

        public MdDistMdObject(string _symbol = "",
                        int _bidQty = -1,
                        int _askQty = -1,
                        double _bidPrice = 0.0,
                        double _askPrice = 0.0,
                        int _lastTradeQty = -1,
                        double _lastTradePrice = 0.0,
                        long _quoteTime = 0,
                        long _tradeTime = 0)
        {
            BidQty = _bidQty;
            AskQty = _askQty;
            BidPrice = _bidPrice;
            AskPrice = _askPrice;
            LastTradeQty = _lastTradeQty;
            LastTradePrice = _lastTradePrice;
            quoteTime = _quoteTime;
            tradeTime = _tradeTime;
            char[] tmpsymbol = _symbol.ToCharArray();
            for (int i = 0; i < 24 && i < _symbol.Length; i++)
            {
                symbol[i] = (byte)tmpsymbol[i];
            }
        }

        public string Symbol
        {
            set
            {
                char[] tmpsymbol = value.ToCharArray();
                unsafe
                {
                    for (int i = 0; i < 24 && i < tmpsymbol.Length; i++)
                    {
                        symbol[i] = (byte)tmpsymbol[i];
                    }
                }
            }

            get
            {
                string tmpsymb;
                char[] tmpchars = new char[24];
                unsafe
                {
                    int i = 0;
                    for (i = 0; i < 24; i++)
                    {
                        if (symbol[i] == '\0')
                        {
                            break;
                        }
                        tmpchars[i] = (char)symbol[i];
                    }
                    tmpsymb = new string(tmpchars).Substring(0, i);
                }
                return tmpsymb;
            }
        }
    }
}

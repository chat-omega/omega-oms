using System.Runtime.InteropServices;

namespace ZeroPlus.Models.SharedMemory
{
    /// <summary>Explicit layout for Native AOT interop (<c>SharedArray&lt;T&gt;</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct HwDistGreekObject
    {
        public fixed byte symbol[24];
        public double Ask;
        public double Bid;
        public long AskTimeStamp;
        public long BidTimeStamp;
        public long TimeStamp;
        public double TV;
        public double IV;
        public double Rho;
        public double Theta;
        public double Vega;
        public double Gamma;
        public double Delta;
        public double Under;
        public double UBid;
        public double UAsk;
        public long PersistorSeqNum;
        public long PersistorTimestamp;
        public int InfoBits;
        public double TimeValue;
        public double IntrinsicValue;
        public double FVDivs;
        public double UTheo;
        public double UFwd;
        public double UFwdFactor;
        public double BorrowCost;
        public double BorrowRate;

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

        public HwDistGreekObject(string symbol = "",
                                 double ask = 0,
                                 double bid = 0,
                                 long askTimeStamp = 0,
                                 long bidTimeStamp = 0,
                                 long timeStamp = 0,
                                 double tv = 0,
                                 double iv = 0,
                                 double rho = 0,
                                 double theta = 0,
                                 double vega = 0,
                                 double gamma = 0,
                                 double delta = 0,
                                 double under = 0,
                                 double uBid = 0,
                                 double uAsk = 0,
                                 long persistorSeqNum = 0,
                                 long persistorTimestamp = 0,
                                 int infoBits = 0,
                                 double timeValue = 0,
                                 double intrinsicValue = 0,
                                 double fVDivs = 0,
                                 double uTheo = 0.0,
                                 double uFwd = 0.0,
                                 double uFwdFactor = 0.0,
                                 double borrowCost = 0.0,
                                 double borrowRate = 0.0)
        {
            Bid = bid;
            Ask = ask;
            AskTimeStamp = askTimeStamp;
            BidTimeStamp = bidTimeStamp;
            TimeStamp = timeStamp;
            TV = tv;
            IV = iv;
            Rho = rho;
            Theta = theta;
            Vega = vega;
            Gamma = gamma;
            Delta = delta;
            Under = under;
            UBid = uBid;
            UAsk = uAsk;
            PersistorSeqNum = persistorSeqNum;
            PersistorTimestamp = persistorTimestamp;
            InfoBits = infoBits;
            TimeValue = timeValue;
            IntrinsicValue = intrinsicValue;
            FVDivs = fVDivs;
            UTheo = uTheo;
            UFwd = uFwd;
            UFwdFactor = uFwdFactor;
            BorrowCost = borrowCost;
            BorrowRate = borrowRate;

            char[] tmpsymbol = symbol.ToCharArray();
            for (int i = 0; i < 24 && i < symbol.Length; i++)
            {
                this.symbol[i] = (byte)tmpsymbol[i];
            }
        }
    }
}

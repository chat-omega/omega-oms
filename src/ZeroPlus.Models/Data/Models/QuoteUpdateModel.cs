using System;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Data.Models;

public class QuoteUpdateModel
{
    public Security Security;
    public ulong TimeStamp;
    public DateTime TimeStampDateTime;
    public double Bid;
    public double Ask;
    public ulong Volume;
    public int BidSize;
    public int AskSize;

    public double Mid => (Bid + Ask) / 2;

    public QuoteUpdateModel(Security security)
    {
        Security = security;
        Bid = double.NaN;
        Ask = double.NaN;
        BidSize = 0;
        AskSize = 0;
        Volume = 0;
        TimeStamp = 0;
        TimeStampDateTime = DateTime.UnixEpoch;
    }

    public void Update(double bid, double ask, int bidSize, int askSize, ulong volume, ulong timestampMillis)
    {
        Bid = bid;
        Ask = ask;
        BidSize = bidSize;
        AskSize = askSize;
        Volume = volume;
        TimeStamp = timestampMillis;
        TimeStampDateTime = timestampMillis.ConvertToWindowsDateTimeWithLocalTimeZone();
    }

    public void Update(double bid, double ask, ulong timestampMillis)
    {
        Bid = bid;
        Ask = ask;
        TimeStamp = timestampMillis;
        TimeStampDateTime = timestampMillis.ConvertToWindowsDateTimeWithLocalTimeZone();
    }

    public override string ToString()
    {
        return "Security: " + Security + ", " +
               "Bid: " + Bid + ", " +
               "Ask: " + Ask + ", " +
               "Mid: " + Mid + ", " +
               "TimeStamp: " + TimeStamp.ToHHMMSSffffff() + ", ";
    }
}
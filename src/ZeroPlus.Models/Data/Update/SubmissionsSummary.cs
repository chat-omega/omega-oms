using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update;

public class SubmissionsSummary
{
    public Broker Broker { get; set; }
    public uint BrokerTotalSubmissions { get; set; }
    public uint BrokerUniqueSubmissions { get; set; }
    public Exchange Exchange { get; set; }
    public uint ExchangeTotalSubmissions { get; set; }
    public uint ExchangeUniqueSubmissions { get; set; }
    public string? Underlying { get; set; }
    public uint UnderlyingTotalSubmissions { get; set; }
    public uint UnderlyingUniqueSubmissions { get; set; }
    public string? Trader { get; set; }
    public uint TraderTotalSubmissions { get; set; }
    public uint TraderUniqueSubmissions { get; set; }
}
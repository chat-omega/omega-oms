using System;

namespace ZeroPlus.Models.Data.Auth;

public class AccessLogDisconnectionEntryModel
{
    public Guid SessionId { get; set; }
    public DateTime DisconnectTime { get; set; }
}

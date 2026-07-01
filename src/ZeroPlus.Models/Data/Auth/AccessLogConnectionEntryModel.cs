using System;

namespace ZeroPlus.Models.Data.Auth;

public class AccessLogConnectionEntryModel
{
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? AppCode { get; set; }
    public string? ClientIp { get; set; }
    public string? SystemInfo { get; set; }
    public DateTime ConnectTime { get; set; }
}

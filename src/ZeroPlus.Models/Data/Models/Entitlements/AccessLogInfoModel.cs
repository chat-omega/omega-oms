using System;

namespace ZeroPlus.Models.Data.Models.Entitlements;

public class AccessLogInfoModel
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public string? HostName { get; set; }
    public string? HostAddress { get; set; }
    public DateTime ConnectTime { get; set; }
    public DateTime DisconnectTime { get; set; }
    public int EntitlementId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public int ResourceId { get; set; }
    public string? Name { get; set; }
    public string? SubGroup { get; set; }
    public string? Description { get; set; }
}

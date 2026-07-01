using System;

namespace ZeroPlus.Models.Data.Models.Entitlements;

public class EntitlementChangeLogModel
{
    public int Id { get; set; }
    public int EntitlementId { get; set; }
    public int? PrevUserId { get; set; }
    public int? PrevResourceId { get; set; }
    public DateTime? PrevActivationTime { get; set; }
    public DateTime? PrevDeactivationTime { get; set; }
    public DateTime? PrevLastUpdateTime { get; set; }
    public int? PrevSimultaneous { get; set; }
    public int? NewUserId { get; set; }
    public int? NewResourceId { get; set; }
    public DateTime? NewActivationTime { get; set; }
    public DateTime? NewDeactivationTime { get; set; }
    public DateTime? NewLastUpdateTime { get; set; }
    public int? NewSimultaneous { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
}

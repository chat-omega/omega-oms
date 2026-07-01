using System;

namespace ZeroPlus.Models.Data.Models.Entitlements;

public class EntitlementInfoModel
{
    public int Id { get; set; }
    public int Simultaneous { get; set; }
    public DateTime? ActivationTime { get; set; }
    public DateTime? DeactivationTime { get; set; }
    public DateTime? LastUpdateTime { get; set; }
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

using System;

namespace ZeroPlus.Models.Data.Auth;

public class AuthConfigSaveModel
{
    public int RequestId { get; set; }
    public int ConfigId { get; set; }
    public int OwnerId { get; set; }
    public int ModuleId { get; set; }
    public DateTime SaveTime { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
}

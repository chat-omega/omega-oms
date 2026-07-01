using System;

namespace ZeroPlus.Models.Data.Auth;

public class AuthLoginResponseModel
{
    public int RequestId { get; set; }
    public bool IsAuthenticated { get; set; }
    public int UserId { get; set; }
    public DateTime ServerTime { get; set; }
    public int MaxDuplicateSessions { get; set; }
    public string AuthCode { get; set; } = string.Empty;
    public string UserJson { get; set; } = string.Empty;
}

namespace ZeroPlus.Models.Data.Auth;

public class AuthLoginRequestModel
{
    public int RequestId { get; set; }
    public bool IsReauth { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SystemInfo { get; set; } = string.Empty;
    public string AuthCode { get; set; } = string.Empty;
}

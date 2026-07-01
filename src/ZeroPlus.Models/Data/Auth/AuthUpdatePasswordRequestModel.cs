namespace ZeroPlus.Models.Data.Auth;

public class AuthUpdatePasswordRequestModel
{
    public int RequestId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}

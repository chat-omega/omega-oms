namespace ZeroPlus.Models.Data.Auth;

public class AuthUpdatePasswordResponseModel
{
    public int RequestId { get; set; }
    public bool IsSuccess { get; set; }
    public string Comment { get; set; } = string.Empty;
}

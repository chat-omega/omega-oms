namespace ZeroPlus.Models.Data.Auth;

public class AuthGetConfigsResponseModel
{
    public int RequestId { get; set; }
    public string ConfigsJson { get; set; } = string.Empty;
}

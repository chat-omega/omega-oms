namespace ZeroPlus.Models.Data.Auth;

public class AuthGetUsersResponseModel
{
    public int RequestId { get; set; }
    public string UsersJson { get; set; } = string.Empty;
}

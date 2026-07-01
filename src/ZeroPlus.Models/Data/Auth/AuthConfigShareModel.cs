namespace ZeroPlus.Models.Data.Auth;

public class AuthConfigShareModel
{
    public int RequestId { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
    public string ReceiverIdsJson { get; set; } = string.Empty;
}

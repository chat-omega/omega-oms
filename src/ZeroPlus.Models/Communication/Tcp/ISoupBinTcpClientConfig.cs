namespace ZeroPlus.Models.Communication.Tcp;

public interface ISoupBinTcpClientConfig
{
    string ServerAddress { get; set; }
    int ServerPort { get; set; }
    int ReceiveBufferSize { get; set; }
    int SendBufferSize { get; set; }
    string SessionId { get; set; }
    string SessionUsername { get; set; }
    string SessionPassword { get; set; }
}

namespace EMAServer.Client
{
    public interface IConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}

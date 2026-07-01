using ZeroPlus.Databento.Client.Config.Interfaces;

namespace ZeroPlus.Databento.Client.Config
{
    public class DatabentoClientConfig : IDatabentoClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}

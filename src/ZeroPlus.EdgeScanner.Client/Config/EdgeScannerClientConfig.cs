
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;

namespace ZeroPlus.EdgeScanner.Client.Config
{
    public class EdgeScannerClientConfig : IEdgeScannerClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9200;
    }
}

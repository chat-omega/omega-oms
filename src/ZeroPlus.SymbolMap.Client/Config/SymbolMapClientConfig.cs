
using ZeroPlus.SymbolMap.Client.Config.Interfaces;

namespace ZeroPlus.SymbolMap.Client.Config
{
    public class SymbolMapClientConfig : ISymbolMapClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9300;
    }
}

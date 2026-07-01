
using System.Collections.Generic;

namespace ZeroPlus.Trades.Client.Config.Interfaces
{
    public interface ITradesClientConfigParser
    {
        List<string> GetSavedConfigsList();
        ITradesClientConfig Parse(string configPath);
    }
}

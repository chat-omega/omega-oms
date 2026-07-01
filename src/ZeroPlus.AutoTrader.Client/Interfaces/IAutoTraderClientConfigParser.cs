
using System.Collections.Generic;

namespace ZeroPlus.AutoTrader.Client.Config.Interfaces
{
    public interface IAutoTraderClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IAutoTraderClientConfig Parse(string configPath);
    }
}

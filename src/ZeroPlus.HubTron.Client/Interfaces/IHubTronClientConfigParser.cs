using System.Collections.Generic;

namespace ZeroPlus.HubTron.Client.Config.Interfaces
{
    public interface IHubTronClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IHubTronClientConfig Parse(string configPath);
    }
}

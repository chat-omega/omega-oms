using System.Collections.Generic;

namespace ZeroPlus.IbGateway.Client.Config.Interfaces
{
    public interface IIbGatewayClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IIbGatewayClientConfig Parse(string configPath);
    }
}

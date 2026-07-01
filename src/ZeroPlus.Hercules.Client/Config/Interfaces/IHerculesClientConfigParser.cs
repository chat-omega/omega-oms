
using System.Collections.Generic;

namespace ZeroPlus.Hercules.Client.Config.Interfaces
{
    public interface IHerculesClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IHerculesClientConfig Parse(string configPath);
    }
}

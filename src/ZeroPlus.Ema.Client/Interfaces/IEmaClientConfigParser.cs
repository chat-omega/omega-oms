using System.Collections.Generic;

namespace ZeroPlus.Ema.Client.Config.Interfaces
{
    public interface IEmaClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IEmaClientConfig Parse(string configPath);
    }
}

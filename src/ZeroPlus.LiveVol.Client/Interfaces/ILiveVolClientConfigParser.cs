using System.Collections.Generic;

namespace ZeroPlus.LiveVol.Client.Config.Interfaces
{
    public interface ILiveVolClientConfigParser
    {
        List<string> GetSavedConfigsList();
        ILiveVolClientConfig Parse(string configPath);
    }
}

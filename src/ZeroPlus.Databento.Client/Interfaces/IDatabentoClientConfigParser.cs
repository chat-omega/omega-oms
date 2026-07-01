using System.Collections.Generic;

namespace ZeroPlus.Databento.Client.Config.Interfaces
{
    public interface IDatabentoClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IDatabentoClientConfig Parse(string configPath);
    }
}

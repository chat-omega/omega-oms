
using System.Collections.Generic;

namespace ZeroPlus.SymbolMap.Client.Config.Interfaces
{
    public interface ISymbolMapClientConfigParser
    {
        List<string> GetSavedConfigsList();
        ISymbolMapClientConfig Parse(string configPath);
    }
}

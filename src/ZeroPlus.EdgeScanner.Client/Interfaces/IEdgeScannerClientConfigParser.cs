
using System.Collections.Generic;

namespace ZeroPlus.EdgeScanner.Client.Config.Interfaces
{
    public interface IEdgeScannerClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IEdgeScannerClientConfig Parse(string configPath);
    }
}

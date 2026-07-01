using System.Collections.Generic;

namespace ZeroPlus.Interpolator.Client.Config.Interfaces
{
    public interface IInterpolatorClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IInterpolatorClientConfig Parse(string configPath);
    }
}

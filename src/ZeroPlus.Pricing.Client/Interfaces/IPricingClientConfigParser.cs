using System.Collections.Generic;

namespace ZeroPlus.Pricing.Client.Config.Interfaces
{
    public interface IPricingClientConfigParser
    {
        List<string> GetSavedConfigsList();
        IPricingClientConfig Parse(string configPath);
    }
}

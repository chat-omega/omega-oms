using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZeroPlus.AutoTrader.Client.Config;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;

namespace ZeroPlus.Oms.Config
{
    public class AutoTraderDirectClientConfig : AutoTraderClientConfig
    {
        public AutoTraderDirectClientConfig(ILogger<IAutoTraderClientConfig> logger, IConfiguration configuration) : base(logger, configuration)
        {
            configuration.GetSection(nameof(AutoTraderDirectClientConfig)).Bind(this);
        }
    }
}

using Microsoft.Extensions.Logging;
using ZeroPlus.AutoTrader.Client;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;

namespace ZeroPlus.Oms.Factories
{
    public class AutoTraderClientFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ISbeMessageDecoder _sbeMessageDecoder;
        private readonly ISbeMessageEncoder _sbeMessageEncoder;

        public AutoTraderClientFactory(ILoggerFactory loggerFactory, 
            ISbeMessageDecoder sbeMessageDecoder,
            ISbeMessageEncoder sbeMessageEncoder)
        {
            _loggerFactory = loggerFactory;
            _sbeMessageDecoder = sbeMessageDecoder;
            _sbeMessageEncoder = sbeMessageEncoder;
        }

        public IAutoTraderClient CreateAutoTraderClient(IAutoTraderClientConfig autoTraderClientConfig)
        {
            return new AutoTraderClient(
                _loggerFactory.CreateLogger<AutoTraderClient>(),
                autoTraderClientConfig,
                _sbeMessageDecoder,
                _sbeMessageEncoder);
        }
    }
}

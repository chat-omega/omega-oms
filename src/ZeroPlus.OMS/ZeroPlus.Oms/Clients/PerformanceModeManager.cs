using System.Collections.Generic;
using ZeroPlus.Databento.Client.Interfaces;
using ZeroPlus.Ema.Client.Interfaces;
using ZeroPlus.Raptor.Client.Interfaces;

namespace ZeroPlus.Oms.Clients
{
    public class PerformanceModeManager
    {
        private List<IRaptorClient> _raptorClients = [];
        private IEmaClient _emaClient;
        private IDatabentoClient _databentoClient;
        private QuoteClient _quoteClient;

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public void Initialize(List<IRaptorClient> raptorClients)
        {
            _raptorClients = raptorClients;
        }

        public void Initialize(IEmaClient client)
        {
            _emaClient = client;
        }

        public void Initialize(IDatabentoClient client)
        {
            _databentoClient = client;
        }

        public void Initialize(QuoteClient client)
        {
            _quoteClient = client;
        }

        public void OnPerformanceModeChanged(bool isPerformanceEnabled)
        {
            foreach (var raptorClient in _raptorClients)
            {
                raptorClient.SetPerformanceMode(isPerformanceEnabled);
            }
            _emaClient.SetPerformanceMode(isPerformanceEnabled);
            _databentoClient.SetPerformanceMode(isPerformanceEnabled);
            _quoteClient.ThrottleMarketData(isPerformanceEnabled);
        }
    }
}

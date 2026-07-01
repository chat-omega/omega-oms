using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Trades.Client.Interfaces;

namespace ZeroPlus.Oms.Clients
{
    public class TradesClient : ClientBase
    {
        private readonly ILogger<TradesClient> _logger;
        private readonly ConcurrentDictionary<int, ITradesSubscriber> _tradeRequestIdToSubscriberMap = new();
        private int _requestID = 0;

        public ITradesClient Client { get; }
        public TradesClient(ILogger<TradesClient> logger, ITradesClient client)
        {
            _logger = logger;
            Client = client;
            Client.ClientConnected += OnClient_ClientConnected;
            Client.ClientDisconnected += OnClient_ClientDisconnected;
        }

        public async Task RequestTrades(string symbolList, bool mleg, bool realTime, DateTime startTime, DateTime endTime, string constraint1, string constraint2, int deltaAdjEdgeInterval, ITradesSubscriber tradesSubscriber, bool matchIoiTrades)
        {
            var (underlyingSymbols, symbols) = ParseSymbolList(symbolList);
            var requestId = GetNextRequestId();
            var request = new OpraDatabaseTradesRequest(requestId, underlyingSymbols, symbols, mleg, realTime, startTime, endTime, constraint1, constraint2, deltaAdjEdgeInterval, false, matchIoiTrades);
            _tradeRequestIdToSubscriberMap[requestId] = tradesSubscriber;
            var response = await Client.RequestTrades(request);
            HandleTradesMessage(requestId, response.Trades);
        }

        public async Task StopTrades(string symbolList, bool mleg, bool realTime, DateTime startTime, DateTime endTime, string constraint1, string constraint2, int deltaAdjEdgeInterval, ITradesSubscriber tradesSubscriber)
        {
            var (underlyingSymbols, symbols) = ParseSymbolList(symbolList);
            var requestId = GetNextRequestId();
            var request = new OpraDatabaseTradesRequest(requestId, underlyingSymbols, symbols, mleg, realTime, startTime, endTime, constraint1, constraint2, deltaAdjEdgeInterval, true, false);
            _tradeRequestIdToSubscriberMap[requestId] = tradesSubscriber;
            await Client.RequestTrades(request);
            _tradeRequestIdToSubscriberMap.TryRemove(tradesSubscriber.RequestId, out ITradesSubscriber _);
        }

        private static (List<string> UnderlyingSymbols, List<string> Symbols) ParseSymbolList(string symbolList)
        {
            List<string> symbols = [];
            List<string> underlyingSymbols = [];
            if (string.IsNullOrEmpty(symbolList))
                return (underlyingSymbols, symbols);

            var splitSymbols = symbolList.ToUpper().Split(',').ToList();
            foreach (var symbol in splitSymbols)
            {
                // This is an equity or index
                if ((symbol[0] >= 65 && symbol[0] <= 90) || symbol[0] == 36)
                {
                    underlyingSymbols.Add(symbol);
                }
                else
                {
                    symbols.Add(symbol);
                }
            }

            return (underlyingSymbols, symbols);
        }

        private int GetNextRequestId()
        {
            return Interlocked.Increment(ref _requestID);
        }

        private void HandleTradesMessage(int requestId, List<OpraDatabaseTradeModel> mdTrades)
        {
            try
            {
                if (_tradeRequestIdToSubscriberMap.TryGetValue(requestId, out ITradesSubscriber subscriber))
                {
                    if (subscriber.CancellationToken.IsCancellationRequested)
                    {
                        _tradeRequestIdToSubscriberMap.TryRemove(requestId, out _);
                    }
                    else
                    {
                        var nullIois = mdTrades.Where(t => (t.IoiModel?.Legs.Count ?? 0) == 0);
                        foreach (var nullIoiTrade in nullIois)
                        {
                            nullIoiTrade.IoiModel = null;
                        }
                        subscriber.QueueTrades(mdTrades);
                        subscriber.ProcessQueuedTrades();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Received excetion while handling trades.");
            }
        }

        public override bool Start()
        {
            Task.Run(Client.ConnectAndStartAsync);
            return true;
        }

        public override void Stop()
        {
            Task.Run(Client.DisconnectAndStopAsync);
        }

        protected override void RegisterClient()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Client.RegisterClientAsync(Username, AppId, version!, Dns.GetHostName());
        }
    }
}

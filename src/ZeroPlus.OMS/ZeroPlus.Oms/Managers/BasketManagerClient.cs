using NLog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.BasketManager;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using IOrder = ZeroPlus.Models.Data.Trading.Interfaces.IOrder;

namespace ZeroPlus.Oms.Managers
{
    public class BasketManagerClient
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsConfig _config;
        private readonly CommsClient _commsClient;
        private readonly ConcurrentDictionary<string, IBasket> _basketIdToBasketMap = new();

        public bool IsConnected { get; set; }
        public bool SameComputer { get; set; }


        public BasketManagerClient(OmsConfig config, OmsCore omsCore)
        {
            _config = config;
            _commsClient = new CommsClient(OmsConfig.ManagerGuid, config, HandleMessage, omsCore, register: false);
            _commsClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
        }

        public async Task<bool> StartAsync()
        {
            _log.Info(nameof(StartAsync));
            return await Task.Run(() => _commsClient.Start(_config.BasketManagerOmsAddress, _config.BasketManagerOmsPort));
        }

        public async Task StopAsync()
        {
            _log.Info(nameof(StopAsync));
            await Task.Run(() => _commsClient.Stop());
        }

        private void HandleMessage(Message message)
        {
            try
            {
                switch (message.Template.TemplateType)
                {
                    case TemplateType.BasketCommand:
                        BasketCommand basketCommand = MessageFactory.DecodeBasketCommandMessage(message);
                        HandleCommand(basketCommand);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleMessage));
            }
        }

        private void OnConnectionStatusChangedEvent(bool connected)
        {
            if (connected)
            {
                foreach (IBasket basket in _basketIdToBasketMap.Values.ToList())
                {
                    Update(basket);
                }

                SameComputer = CommsClient.IsLocalAddress(_config.BasketManagerOmsAddress);
            }

            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(connected);
        }

        public void Update(IBasket basket)
        {
            try
            {
                _basketIdToBasketMap[basket.Uid] = basket;
                BasketUpdate basketUpdate = new()
                {
                    Uid = basket.Uid,
                    InstanceId = basket.InstanceId,
                    Title = basket.ModuleTitle,
                    Username = basket.Username,
                    Host = basket.Host,
                    RowCount = basket.RowCount,
                    Fills = basket.Fills,
                    EdgeType = basket.GetEdgeType(),
                    Edge = basket.GetEdge(),
                    OpenTicketEnabled = basket.GetOpenTicketState(),
                    ResubmitTimer = basket.ResubmitCountDown.TotalSeconds,
                    ResubmitTimerInterval = basket.ResubmitIntervalSec,
                    ResubmitTimerOn = basket.ResubmitOnTimer,
                    SampleDescription = basket.SampleDescription,
                    Tag = basket.Tag,
                };
                _commsClient.SendBasketUpdateMessage(basketUpdate);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        public void TradeUpdate(IBasket basket, IOmsOrder trade)
        {
            try
            {
                BasketTradeUpdate basketTradeUpdate = new()
                {
                    Uid = basket.Uid,
                    SpreadId = trade.SpreadId,
                    Price = trade.Price,
                    AveragePrice = trade.AveragePrice,
                    LastQuantity = trade.LastQuantity,
                    LastUpdateTime = trade.LastUpdateTime == DateTime.MinValue || trade.LastUpdateTime.Date == new DateTime(1970, 1, 1).Date ? "" : trade.LastUpdateTime.ToString("hh:mm:ss.ffff"),
                };
                _commsClient.SendBasketTradeUpdateMessage(basketTradeUpdate);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        public bool OpenTicket(IBasket basket, IOrder order, bool closing)
        {
            try
            {
                if (!IsConnected || SameComputer)
                {
                    return false;
                }

                OpenTicketRequest request = new()
                {
                    Symbol = order.Symbol,
                    Side = order.Side.ToString(),
                    Price = order.Price,
                    UnderPrice = order.UnderMid,
                    Edge = order.Edge,
                    Route = order.Route,
                    WithContra = closing,
                };
                _commsClient.SendOpenTicketRequestMessage(request);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenTicket));
                return false;
            }
        }

        public void Disconnect(IBasket basket)
        {
            try
            {
                BasketCommand basketCommand = new()
                {
                    Id = basket.Uid,
                    Command = BasketCommands.Disconnected,
                };
                _commsClient.SendBasketCommandMessage(basketCommand);
                _basketIdToBasketMap.TryRemove(basket.Uid, out IBasket _);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Disconnect));
            }
        }

        private void HandleCommand(BasketCommand basketCommand)
        {
            if (_basketIdToBasketMap.TryGetValue(basketCommand.Id, out IBasket basket))
            {
                switch (basketCommand.Command)
                {
                    case BasketCommands.ReverseCP when !basket.IsEdgeScanFeedAutoTrader:
                        basket.ReverseSidesNoCheck();
                        break;
                    case BasketCommands.FlipCP when !basket.IsEdgeScanFeedAutoTrader:
                        basket.FlipCpNoCheck();
                        break;
                    case BasketCommands.OppCP when !basket.IsEdgeScanFeedAutoTrader:
                        basket.OppCpNoCheck();
                        break;
                    case BasketCommands.SubmitAll when !basket.IsEdgeScanFeedAutoTrader:
                        basket.SubmitAllNoCheckSafe();
                        break;
                    case BasketCommands.ModifyAll when !basket.IsEdgeScanFeedAutoTrader:
                        basket.ModifyAllNoCheck();
                        break;
                    case BasketCommands.SetEdge when !basket.IsEdgeScanFeedAutoTrader:
                        basket.SetEdge(basketCommand.Arguments, basketCommand.Argument);
                        break;
                    case BasketCommands.ResubmitOn when !basket.IsEdgeScanFeedAutoTrader:
                        basket.EnableResubmitTimer((int)basketCommand.Argument);
                        break;
                    case BasketCommands.ResubmitOff when !basket.IsEdgeScanFeedAutoTrader:
                        basket.DisableResubmitTimer((int)basketCommand.Argument);
                        break;
                    case BasketCommands.ResetTimer when !basket.IsEdgeScanFeedAutoTrader:
                        basket.ResetTimerNoCheck();
                        break;
                    case BasketCommands.StopAllLoops when !basket.IsEdgeScanFeedAutoTrader:
                        basket.StopAllLoops();
                        break;
                    case BasketCommands.Close when !basket.IsEdgeScanFeedAutoTrader:
                        basket.Close();
                        break;
                    case BasketCommands.Clean:
                        basket.CleanInvalidRows(false);
                        break;
                    case BasketCommands.CancelAll:
                        basket.CancelAllNoCheck();
                        break;
                    case BasketCommands.ClearQty:
                        basket.ClearQty();
                        break;
                    case BasketCommands.OpenTicketOn:
                        basket.EnableOpenTicket();
                        break;
                    case BasketCommands.OpenTicketOff:
                        basket.DisableOpenTicket();
                        break;
                    case BasketCommands.Activate:
                        basket.Activate();
                        break;
                    case BasketCommands.Hide:
                        basket.Hide();
                        break;
                    // Ignore
                    case BasketCommands.OpenTicketOnHost:
                        break;
                    case BasketCommands.OpenTicketOnManager:
                        break;
                    case BasketCommands.Disconnected:
                        break;
                }
            }
        }
    }
}

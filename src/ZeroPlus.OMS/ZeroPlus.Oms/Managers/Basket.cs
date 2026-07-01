using Middleware.Communication.Tcp;
using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.BasketManager;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Comms.Models.Protocols.FAST.Codec;

namespace ZeroPlus.Oms.Managers
{
    public class Basket : IBasket
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public bool IsEdgeScanFeedAutoTrader { get; set; }
        public string Uid { get; set; }
        public string ModuleTitle { get; set; }
        public string Username { get; set; }
        public string Host { get; set; }
        public string Setup { get; set; }
        public string List { get; set; }
        public int RowCount { get; set; }
        public int Fills { get; set; }
        public TimeSpan ResubmitCountDown { get; set; }
        public int ResubmitIntervalSec { get; set; }
        public bool ResubmitOnTimer { get; set; }
        public double Edge { get; set; }
        public string EdgeType { get; set; }
        public bool OpenTicket { get; set; }
        public TcpSocket TcpSocket { get; internal set; }
        public string InstanceId { get; set; }
        public string SampleDescription { get; set; }
        public string Tag { get; set; }

        public void CancelAllNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.CancelAll,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllNoCheck));
            }
        }

        public Task CleanInvalidRows(bool withUndoPrompt)
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.Clean,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CleanInvalidRows));
                return Task.FromException(ex);
            }
        }

        public void ClearQty()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ClearQty,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearQty));
            }
        }

        public void EnableResubmitTimer(int interval)
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ResubmitOn,
                        Argument = interval,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EnableResubmitTimer));
            }
        }

        public void DisableResubmitTimer(int interval)
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ResubmitOff,
                        Argument = interval,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisableResubmitTimer));
            }
        }

        public void FlipCpNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.FlipCP,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FlipCpNoCheck));
            }
        }

        public double GetEdge()
        {
            return Edge;
        }

        public string GetEdgeType()
        {
            return EdgeType;
        }

        public bool GetOpenTicketState()
        {
            return OpenTicket;
        }

        public Task ModifyAllNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ModifyAll,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifyAllNoCheck));
            }
            return Task.CompletedTask;
        }

        public void OppCpNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.OppCP,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OppCpNoCheck));
            }
        }

        public void ReverseSidesNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ReverseCP,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseSidesNoCheck));
            }
        }

        public void SetEdge(string edgeType, double edge)
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.SetEdge,
                        Arguments = edgeType,
                        Argument = edge,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetEdge));
            }
        }

        public Task SubmitAllNoCheckSafe()
        {
            return SubmitAllNoCheck();
        }

        public Task SubmitAllNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.SubmitAll,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitAllNoCheck));
            }
            return Task.CompletedTask;
        }

        public void EnableOpenTicket()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.OpenTicketOn,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EnableOpenTicket));
            }
        }

        public void DisableOpenTicket()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.OpenTicketOff,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisableOpenTicket));
            }
        }

        public void EnableTicketProxy()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.OpenTicketOnManager,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EnableOpenTicket));
            }
        }

        public void DisableTicketProxy()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.OpenTicketOnHost,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisableOpenTicket));
            }
        }

        public void ResetTimerNoCheck()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.ResetTimer,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetTimerNoCheck));
            }
        }

        public void StopAllLoops()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.StopAllLoops,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllLoops));
            }
        }

        public void Activate()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.Activate,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllLoops));
            }
        }

        public void Hide()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.Hide,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllLoops));
            }
        }

        public void Close()
        {
            try
            {
                if (TcpSocket != null)
                {
                    BasketCommand command = new()
                    {
                        Id = Uid,
                        Command = BasketCommands.Close,
                    };
                    TcpSocket.SendAsync(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(command)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllLoops));
            }
        }
    }
}
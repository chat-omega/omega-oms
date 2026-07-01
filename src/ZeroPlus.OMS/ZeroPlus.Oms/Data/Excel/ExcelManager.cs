using Middleware.Communication.Tcp;
using NLog;
using System;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Comms.Models.Protocols.FAST.Codec;

namespace ZeroPlus.Oms.Data.Excel
{
    public class ExcelManager
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public string Id;
        public TcpSocket Socket { get; internal set; }
        public string Host { get; internal set; }

        public ExcelManager()
        {
            Id = Guid.NewGuid().ToString();
        }

        public void StartExcelInstance()
        {
            try
            {
                if (Socket != null && !Socket.IsShutdown)
                {
                    DomCommand domStart = new()
                    {
                        Command = Command.Start
                    };
                    Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartExcelInstance));
            }
        }

    }
}

using NLog;
using System;
using System.Threading;
using ZeroPlus.Comms.Helper.Concurrency;
using ZeroPlus.Comms.Helper.Utilities;
using ZeroPlus.Comms.Models.Protocols.FAST;

namespace ZeroPlus.Oms.Clients
{
    public class MessageHandler
    {
        public delegate void HandleMessageHandler(Message message);

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private ProducerConsumer _messagePump;
        protected Thread _messagePumpThread;
        private readonly HandleMessageHandler _handleMessageDelegate;

        public MessageHandler(HandleMessageHandler handleMessagedelegate)
        {
            _handleMessageDelegate += handleMessagedelegate;
            SetupMessageHandlerThread();
        }

        public void SetupMessageHandlerThread()
        {
            try
            {
                StopMessageHandlerThread();
                _messagePump = new ProducerConsumer();
                _messagePumpThread = new Thread(new ThreadStart(MessagePumpHandler))
                {
                    IsBackground = true,
                    CurrentCulture = CultureHelper.DefaultCulture,
                    CurrentUICulture = CultureHelper.DefaultCulture
                };
                _messagePumpThread.Start();
                Thread.CurrentThread.CurrentCulture = CultureHelper.DefaultCulture;
                Thread.CurrentThread.CurrentUICulture = CultureHelper.DefaultCulture;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SetupMessageHandlerThread)} -> Exception setting up thread.");
            }
        }

        public void StopMessageHandlerThread()
        {
            try
            {
                if (_messagePumpThread != null)
                {
                    _messagePump.Produce(null);
                    _messagePumpThread.Join();
                    _messagePumpThread = null;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(StopMessageHandlerThread)} -> Exception stopping message handler.");
            }
        }

        internal void AddMessage(Message message)
        {
            try
            {
                _messagePump.Produce(message);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(AddMessage)}");
            }
        }

        private void MessagePumpHandler()
        {
            Message message;
            while ((message = (Message)_messagePump.Consume()) != null)
            {
                if (message.Template.TemplateType != TemplateType.Heartbeat)
                {
                    ProcessMessage(message);
                }
            }
        }

        private void ProcessMessage(Message message)
        {
            try
            {
                _handleMessageDelegate?.Invoke(message);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(MessagePumpHandler)}. Template: {message.Template.TemplateType}");
            }
        }

        internal void Dispose()
        {
            try
            {
                StopMessageHandlerThread();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(Dispose)}");
            }
        }
    }
}

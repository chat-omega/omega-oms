using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.SoupBinTCP.Messages;

namespace ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces
{
    public interface ISoupBinTcpEncoder
    {
        IMessageSender? Sender { get; }
        int MsgQueueCount { get; }

        void StartEngine();
        void StopEngine();
        /// <summary>
        /// Queues a topic for sending.
        /// </summary>
        /// <param name="topic">The topic to send.</param>
        /// <param name="sendCache">If true, the topic is added to the processing queue; if false, only the topic index is updated.</param>
        void Send(ITopic? topic, bool sendCache);
        void Reset(ITopic? topic);
    }
}
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class DeltaAdjTheoDetailsUpdateTopic : IDeltaAdjTheoDetailsUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<SecurityDecimalFieldUpdateTopic> _logger;
        private ulong _index;
        private int _id;
        private AdjTheoUpdate _lastUpdate;
        private byte _modelId;

        public Security? Security { get; set; }
        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public DeltaAdjTheoDetailsUpdateTopic(ILogger<SecurityDecimalFieldUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void Initialize(Security security, int id, byte modelId)
        {
            _id = id;
            _modelId = modelId;
            Security = security;
        }

        public void FieldUpdated(uint sequence,
                                 double deltaAdjustedTheo,
                                 double smoothedDeltaAdjustedTheo,
                                 double underlying,
                                 bool jumpDetected,
                                 double secondaryTheo,
                                 double secondaryTheoAdj,
                                 double priceMetric,
                                 double secondaryVol,
                                 double changeInPremium,
                                 double secondarySpot,
                                 double daEma,
                                 double volaEma)
        {
            if (_lastUpdate.Sequence != sequence)
            {
                _lastUpdate = new AdjTheoUpdate(
                    _id,
                    sequence,
                    deltaAdjustedTheo,
                    smoothedDeltaAdjustedTheo,
                    underlying,
                    jumpDetected,
                    secondaryTheo,
                    secondaryTheoAdj,
                    priceMetric,
                    _modelId,
                    secondaryVol,
                    changeInPremium,
                    secondarySpot,
                    daEma,
                    volaEma);
                Interlocked.Increment(ref _index);
            }
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;
                if (lastIndex == index || Security == null)
                {
                    return false;
                }

                int written = ctx.Encoder.EncodeDeltaAdjTheoUpdate(ctx.DirectBuffer, 0, ref _lastUpdate);
                sender.SendEncoded(encodeContext, written, Compressed);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }
    }
}

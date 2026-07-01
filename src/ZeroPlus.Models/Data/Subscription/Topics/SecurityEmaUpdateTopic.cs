using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class SecurityEmaUpdateTopic : ISecurityEmaUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;

        private readonly ILogger<SecurityEmaUpdateTopic> _logger;
        private ulong _index;

        private EmaUpdateModel _emaUpdateModel;

        public int SecurityId { get; set; }
        public SubscriptionFieldType UpdateType { get; set; }

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public SecurityEmaUpdateTopic(ILogger<SecurityEmaUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;
            _emaUpdateModel = new EmaUpdateModel();
            SecurityId = -1;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;

        }

        public void Init(int securityId, SubscriptionFieldType fieldType)
        {
            SecurityId = securityId;
            UpdateType = fieldType;
        }

        public void FieldUpdated(EmaUpdateModel emaUpdateModel)
        {
            FieldUpdated(emaUpdateModel.Sequence,
                         emaUpdateModel.LowPeriodEma,
                         emaUpdateModel.LowPeriodEmaAdj,
                         emaUpdateModel.LowPeriodEmaUnderlying,
                         emaUpdateModel.MidPeriodEma,
                         emaUpdateModel.MidPeriodEmaAdj,
                         emaUpdateModel.MidPeriodEmaUnderlying,
                         emaUpdateModel.MidPeriodBidEma,
                         emaUpdateModel.MidPeriodBidEmaAdj,
                         emaUpdateModel.MidPeriodAskEma,
                         emaUpdateModel.MidPeriodAskEmaAdj,
                         emaUpdateModel.HighPeriodEma,
                         emaUpdateModel.HighPeriodEmaAdj,
                         emaUpdateModel.HighPeriodEmaUnderlying,
                         emaUpdateModel.QuoteTimestampNanos,
                         emaUpdateModel.CalculationTimestampNanos,
                         emaUpdateModel.LowPeriodEmaTimestampNanos,
                         emaUpdateModel.MidPeriodEmaTimestampNanos,
                         emaUpdateModel.HighPeriodEmaTimestampNanos);
        }

        public void FieldUpdated(ulong sequence,
                                 double lowPeriodEma,
                                 double lowPeriodEmaAdj,
                                 double lowPeriodEmaUnderlying,
                                 double midPeriodEma,
                                 double midPeriodEmaAdj,
                                 double midPeriodEmaUnderlying,
                                 double midPeriodBidEma,
                                 double midPeriodBidEmaAdj,
                                 double midPeriodAskEma,
                                 double midPeriodAskEmaAdj,
                                 double highPeriodEma,
                                 double highPeriodEmaAdj,
                                 double highPeriodEmaUnderlying,
                                 ulong quoteTimestampNanos = 0,
                                 ulong calculationTimestampNanos = 0,
                                 ulong lowPeriodEmaTimestampNanos = 0,
                                 ulong midPeriodEmaTimestampNanos = 0,
                                 ulong highPeriodEmaTimestampNanos = 0)
        {
            _emaUpdateModel.Sequence = sequence;
            _emaUpdateModel.LowPeriodEma = lowPeriodEma;
            _emaUpdateModel.LowPeriodEmaAdj = lowPeriodEmaAdj;
            _emaUpdateModel.LowPeriodEmaUnderlying = lowPeriodEmaUnderlying;
            _emaUpdateModel.MidPeriodEma = midPeriodEma;
            _emaUpdateModel.MidPeriodEmaAdj = midPeriodEmaAdj;
            _emaUpdateModel.MidPeriodEmaUnderlying = midPeriodEmaUnderlying;
            _emaUpdateModel.HighPeriodEma = highPeriodEma;
            _emaUpdateModel.HighPeriodEmaAdj = highPeriodEmaAdj;
            _emaUpdateModel.HighPeriodEmaUnderlying = highPeriodEmaUnderlying;
            _emaUpdateModel.MidPeriodBidEma = midPeriodBidEma;
            _emaUpdateModel.MidPeriodBidEmaAdj = midPeriodBidEmaAdj;
            _emaUpdateModel.MidPeriodAskEma = midPeriodAskEma;
            _emaUpdateModel.MidPeriodAskEmaAdj = midPeriodAskEmaAdj;

            _emaUpdateModel.QuoteTimestampNanos = quoteTimestampNanos;
            _emaUpdateModel.CalculationTimestampNanos = calculationTimestampNanos;
            _emaUpdateModel.LowPeriodEmaTimestampNanos = lowPeriodEmaTimestampNanos;
            _emaUpdateModel.MidPeriodEmaTimestampNanos = midPeriodEmaTimestampNanos;
            _emaUpdateModel.HighPeriodEmaTimestampNanos = highPeriodEmaTimestampNanos;

            GetNextIndex();
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;
                if (lastIndex == index || SecurityId == -1)
                {
                    return false;
                }

                int written = ctx.Encoder.EncodeSecurityEmaUpdate(ctx.DirectBuffer, 0, SecurityId,
                                                                         UpdateType,
                                                                         _emaUpdateModel);
                sender.SendEncoded(encodeContext, written, Compressed);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        private ulong GetNextIndex()
        {
            return Interlocked.Increment(ref _index);
        }
    }
}

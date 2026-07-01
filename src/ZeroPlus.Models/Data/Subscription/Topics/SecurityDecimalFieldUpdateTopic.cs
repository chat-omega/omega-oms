using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class SecurityDecimalFieldUpdateTopic : ISecurityDecimalFieldUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<SecurityDecimalFieldUpdateTopic> _logger;
        private ulong _index;
        private int _symbolIndex;

        private uint _updateSequence;
        private ulong _underlyingTimestamp;
        private ulong _snapshotTimestamp;
        private ulong _hanweckTimestamp;
        private double _theo;
        private double _delta;
        private double _gamma;
        private double _vega;
        private double _theta;
        private double _rho;
        private double _implied;
        private double _latestMidPrice;
        private double _snapshotMidPrice;
        private double _deltaAdjustedTheo;
        private bool _jumpDetected;

        public Security? Security { get; set; }
        public SubscriptionFieldType UpdateType { get; set; }

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }
        public int SymbolIndex { get => _symbolIndex; set => _symbolIndex = value; }

        public SecurityDecimalFieldUpdateTopic(ILogger<SecurityDecimalFieldUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;
            _updateSequence = 0;
            _underlyingTimestamp = (ulong)DateTime.UnixEpoch.Ticks;
            _snapshotTimestamp = (ulong)DateTime.UnixEpoch.Ticks;
            _hanweckTimestamp = (ulong)DateTime.UnixEpoch.Ticks;
            _theo = double.NaN;
            _delta = double.NaN;
            _gamma = double.NaN;
            _vega = double.NaN;
            _theta = double.NaN;
            _rho = double.NaN;
            _implied = double.NaN;
            _latestMidPrice = double.NaN;
            _snapshotMidPrice = double.NaN;
            _deltaAdjustedTheo = double.NaN;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void FieldUpdated(Security security, SubscriptionFieldType fieldType, uint sequence, double update, ulong timestamp, bool jumpDetected)
        {
            if (Security == null)
            {
                Security = security;
                UpdateType = fieldType;
            }
            if (_updateSequence < sequence)
            {
                _updateSequence = sequence;
                _deltaAdjustedTheo = update;
                _underlyingTimestamp = timestamp;
                _jumpDetected = jumpDetected;
                GetNextIndex();
            }
        }

        public void FullDeltaAdjTheoUpdate(Security security,
                                           int id,
                                           SubscriptionFieldType fieldType,
                                           uint updateSequence,
                                           ulong underlyingTimestamp,
                                           ulong snapshotTimestamp,
                                           ulong hanweckTimestamp,
                                           double theo,
                                           double delta,
                                           double gamma,
                                           double vega,
                                           double theta,
                                           double rho,
                                           double implied,
                                           double latestMidPrice,
                                           double snapshotMidPrice,
                                           double deltaAdjustedTheo,
                                           bool jumpDetected)
        {
            if (Security == null)
            {
                Security = security;
                SymbolIndex = id;
                UpdateType = fieldType;
            }
            if (_updateSequence < updateSequence)
            {
                _updateSequence = updateSequence;
                _underlyingTimestamp = underlyingTimestamp;
                _snapshotTimestamp = snapshotTimestamp;
                _hanweckTimestamp = hanweckTimestamp;
                _theo = theo;
                _delta = delta;
                _gamma = gamma;
                _vega = vega;
                _theta = theta;
                _rho = rho;
                _implied = implied;
                _latestMidPrice = latestMidPrice;
                _snapshotMidPrice = snapshotMidPrice;
                _deltaAdjustedTheo = deltaAdjustedTheo;
                _jumpDetected = jumpDetected;
                GetNextIndex();
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

                int written = ctx.Encoder.EncodeDoubleUpdate(ctx.DirectBuffer, 0, SymbolIndex,
                                                                    UpdateType,
                                                                    _updateSequence,
                                                                    _underlyingTimestamp,
                                                                    _snapshotTimestamp,
                                                                    _hanweckTimestamp,
                                                                    _theo,
                                                                    _delta,
                                                                    _gamma,
                                                                    _vega,
                                                                    _theta,
                                                                    _rho,
                                                                    _implied,
                                                                    _latestMidPrice,
                                                                    _snapshotMidPrice,
                                                                    _deltaAdjustedTheo,
                                                                    _jumpDetected);
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

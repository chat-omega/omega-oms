using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class PortfolioUpdateTopic : IPortfolioUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private const int BATCH_UPDATE_SIZE = 65500;
        private readonly ILogger<PortfolioUpdateTopic> _logger;
        private readonly object _indexLock;
        private ulong _index;
        private IPortfolio? _portfolio;
        private IPosition? _mainPosition;
        private ICollection<IPortfolio>? _portfolios;
        private List<IPosition>? _positions;
        private readonly ConcurrentDictionary<ulong, (IPosition, TopicUpdateType)> _indexToPositionUpdateMap;
        private readonly ConcurrentDictionary<IPosition, ulong> _positionToLastUpdateIndexMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public bool IgnoreBreakdownPositions { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public int RequestId { get; private set; }
        public ulong Index { get => _index; set => _index = value; }
        public bool OneTimeUse { get; set; }
        public bool IsUseSlim { get; set; }

        public PortfolioUpdateTopic(ILogger<PortfolioUpdateTopic> logger)
        {
            _logger = logger;
            _indexToPositionUpdateMap = new ConcurrentDictionary<ulong, (IPosition, TopicUpdateType)>();
            _positionToLastUpdateIndexMap = new ConcurrentDictionary<IPosition, ulong>();
            _indexLock = new object();
            Id = Guid.NewGuid();
            Compressed = true;
            MessagePriority = PRIORITY;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;

                if (RequestId > 0)
                {
                    if (_portfolios != null)
                    {
                        int written = ctx.Encoder.EncodeMultiplePortfoliosAddedMessage(ctx.DirectBuffer, 0, RequestId, _portfolios);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        return true;
                    }

                    if (_positions != null && _portfolio != null)
                    {
                        int updatesCounts = _positions.Count;

                        IPosition[] chunk = new IPosition[Math.Min(BATCH_UPDATE_SIZE, updatesCounts)];
                        int itemsCounter = 0;
                        for (int i = 0; i < updatesCounts; i++)
                        {
                            IPosition update = _positions[i];
                            chunk[itemsCounter++] = update;
                            if (itemsCounter == BATCH_UPDATE_SIZE || i == updatesCounts - 1)
                            {
                                int written = ctx.Encoder.EncodeMultiplePositionsAdded(ctx.DirectBuffer, 0, RequestId, _portfolio, chunk);
                                sender.SendEncoded(encodeContext, written, Compressed);
                                chunk = new IPosition[Math.Min(BATCH_UPDATE_SIZE, updatesCounts - i + 1)];
                                itemsCounter = 0;
                            }
                        }
                        return true;
                    }
                }

                if (index == lastIndex || _portfolio == null)
                {
                    return false;
                }

                if (_portfolio.PositionsCount == 1)
                {
                    int written = ctx.Encoder.EncodePortfolioAdded(ctx.DirectBuffer, 0, _portfolio);
                    sender.SendEncoded(encodeContext, written, Compressed);
                    written = ctx.Encoder.EncodePositionAdded(ctx.DirectBuffer, 0, _portfolio, _portfolio.Positions.First());
                    sender.SendEncoded(encodeContext, written, Compressed);
                    written = !IsUseSlim
                        ? ctx.Encoder.EncodePositionUpdated(ctx.DirectBuffer, 0, _portfolio, _portfolio.Positions.ToArray(), index == 0)
                        : ctx.Encoder.EncodePositionUpdatedSlim(ctx.DirectBuffer, 0, _portfolio, _portfolio.Positions.ToArray(), index == 0);
                    sender.SendEncoded(encodeContext, written, Compressed);
                    return true;
                }
                else
                {
                    (List<IPosition> AddedPositions, List<IPosition> UpdatedPositions) updates = GetUpdatesInRange(index, lastIndex, out nextIndex);
                    if (index == nextIndex || (updates.AddedPositions.Count == 0 && updates.UpdatedPositions.Count == 0))
                    {
                        return false;
                    }

                    for (int i = 0; i < updates.AddedPositions.Count; i++)
                    {
                        IPosition position = updates.AddedPositions[i];
                        int written = ctx.Encoder.EncodePositionAdded(ctx.DirectBuffer, 0, _portfolio, position);
                        sender.SendEncoded(encodeContext, written, Compressed);
                    }

                    int updatesCounts = updates.UpdatedPositions.Count;
                    IPosition[] chunk = new IPosition[Math.Min(BATCH_UPDATE_SIZE, updatesCounts)];

                    int count = 0;
                    var isReplay = index == 0;
                    for (int i = 0; i < updatesCounts; i++)
                    {
                        IPosition update = updates.UpdatedPositions[i];
                        chunk[count++] = update;
                        if (count == BATCH_UPDATE_SIZE || i == updatesCounts - 1)
                        {
                            int written = !IsUseSlim
                                ? ctx.Encoder.EncodePositionUpdated(ctx.DirectBuffer, 0, _portfolio, chunk, isReplay)
                                : ctx.Encoder.EncodePositionUpdatedSlim(ctx.DirectBuffer, 0, _portfolio, chunk, isReplay);
                            sender.SendEncoded(encodeContext, written, Compressed);

                            chunk = new IPosition[Math.Min(BATCH_UPDATE_SIZE, updatesCounts - i + 1)];
                            count = 0;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
            finally
            {
                if (OneTimeUse)
                {
                    Clear();
                }
            }
        }

        public void PositionAdded(IPortfolio portfolio, IPosition position)
        {
            _portfolio = portfolio;
            if (!IgnoreBreakdownPositions || position.PositionType is PositionType.Main)
            {
                AddToUpdateMap(position, TopicUpdateType.Add);
                AddToUpdateMap(position, TopicUpdateType.Update);
            }
        }

        public void PositionUpdate(IPortfolio portfolio, ICollection<IPosition> positions)
        {
            _portfolio = portfolio;
            if (positions.Count == 0)
            {
                _mainPosition ??= portfolio.Positions.First();
                AddToUpdateMap(_mainPosition, TopicUpdateType.Update);
            }
            else
            {
                foreach (IPosition? position in positions)
                {
                    if (!IgnoreBreakdownPositions || position.PositionType is PositionType.Main)
                    {
                        AddToUpdateMap(position, TopicUpdateType.Update);
                    }
                }
            }
        }

        public void MultiplePortfoliosAdded(int requestId, ICollection<IPortfolio> portfolios)
        {
            RequestId = requestId;
            _portfolios = portfolios;
        }


        public void MultiplePositionsAdded(int requestId, IPortfolio portfolio, List<IPosition> positions)
        {
            RequestId = requestId;
            _portfolio = portfolio;
            _positions = positions;
        }

        public void Clear()
        {
            if (_portfolios != null)
            {
                foreach (var portfolio in _portfolios)
                {
                    portfolio.Clear();
                }
                _portfolios?.Clear();
            }
            _positionToLastUpdateIndexMap.Clear();
            _indexToPositionUpdateMap.Clear();
        }

        private void AddToUpdateMap(IPosition position, TopicUpdateType updateType)
        {
            bool found = false;
            ulong prevIndex = 0;
            lock (_indexLock)
            {
                _indexToPositionUpdateMap[_index] = (position, updateType);
                if (updateType == TopicUpdateType.Update)
                {
                    found = _positionToLastUpdateIndexMap.TryGetValue(position, out prevIndex);
                    _positionToLastUpdateIndexMap[position] = _index;
                }
                _index++;
            }
            if (found && prevIndex > 0)
            {
                _indexToPositionUpdateMap.TryRemove(prevIndex, out _);
            }
        }

        private (List<IPosition> AddedPositions, List<IPosition> UpdatedPositions) GetUpdatesInRange(ulong index, ulong currentIndex, out ulong nextIndex)
        {
            List<IPosition> addedPositions = new List<IPosition>();
            List<IPosition> updatedPositions = new List<IPosition>();
            ulong count = currentIndex - index;
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToPositionUpdateMap.TryGetValue(index + i, out (IPosition, TopicUpdateType) update))
                    {
                        switch (update.Item2)
                        {
                            case TopicUpdateType.Add:
                                addedPositions.Add(update.Item1);
                                break;
                            case TopicUpdateType.Update:
                                updatedPositions.Add(update.Item1);
                                break;
                        }
                    }
                }
                nextIndex = index + count;
            }
            else
            {
                nextIndex = index;
            }
            return (addedPositions, updatedPositions);
        }
    }
}

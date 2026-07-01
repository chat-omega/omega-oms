using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class BarFeedUpdateTopic : IBarFeedUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        private readonly object _indexLock;
        private ulong _index;
        private Security? _security;
        private double _prevBidUpdate = double.NaN;
        private double _prevAskUpdate = double.NaN;
        private ulong _volume;
        private readonly ILogger<EdgeFeedUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, Tuple<DateTime, double, double, ulong, int, int>> _indexToModelMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public int RequestId { get; set; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public BarFeedUpdateTopic(ILogger<EdgeFeedUpdateTopic> logger)
        {
            _logger = logger;
            _indexToModelMap = new ConcurrentDictionary<ulong, Tuple<DateTime, double, double, ulong, int, int>>();
            _indexLock = new object();

            Id = Guid.NewGuid();
            Compressed = true;
            MessagePriority = PRIORITY;
        }

        public void Init(Security security, TimeSpan cacheTime)
        {
            _security = security;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
                var ctx = (SbeEncodeBufferContext)encodeContext;
                List<Tuple<DateTime, double, double, ulong, int, int>> updates = GetUpdatesSince(index, out nextIndex);
                if (_security == null || index == nextIndex || updates.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < updates.Count; i++)
                {
                    Tuple<DateTime, double, double, ulong, int, int> update = updates[i];

                    DateTime timestamp = update.Item1;
                    double bidUpdate = update.Item2;
                    double askUpdate = update.Item3;

                    ulong volume = update.Item4;

                    int bidSize = update.Item5;
                    int askSize = update.Item6;

                    double bidChange = bidUpdate - _prevBidUpdate;
                    double askChange = askUpdate - _prevAskUpdate;

                    _prevBidUpdate = bidUpdate;
                    _prevAskUpdate = askUpdate;

                    if (bidChange != 0 || askUpdate != 0 || i == updates.Count - 1)
                    {
                        int written = ctx.Encoder.EncodeSecurityDoubleDecimalUpdate(ctx.DirectBuffer, 0, _security.ID, SubscriptionFieldType.Bar, bidUpdate, askUpdate, timestamp, bidChange, askChange, bidSize, askSize, double.NaN);
                        sender.SendEncoded(encodeContext, written, Compressed);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        public void FieldUpdated(double bidUpdate, double askUpdate, DateTime timestamp, int bidSize, int askSize)
        {
            Tuple<DateTime, double, double, ulong, int, int> tuple = Tuple.Create(timestamp, bidUpdate, askUpdate, _volume, bidSize, askSize);
            lock (_indexLock)
            {
                _indexToModelMap[_index] = tuple;
                _index++;
            }
        }

        public void VolumeUpdated(ulong volume)
        {
            _volume = volume;
        }

        public void SaveBar(TimeSpan start, TimeSpan duration)
        {
            try
            {
                ICollection<Tuple<DateTime, double, double, ulong, int, int>> values = _indexToModelMap.Values;
                List<Tuple<DateTime, double, double, ulong, int, int>> saved = new List<Tuple<DateTime, double, double, ulong, int, int>>();
                foreach (Tuple<DateTime, double, double, ulong, int, int> value in values)
                {
                    TimeSpan timestamp = value.Item1.TimeOfDay;
                    if (timestamp >= start && timestamp <= start + duration)
                    {
                        saved.Add(value);
                    }
                }
                string location = GetLocation();
                string export = Newtonsoft.Json.JsonConvert.SerializeObject(saved);
                File.WriteAllText(location, export);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(SaveBar));
            }
        }

        public void LoadBar(TimeSpan start, TimeSpan duration)
        {
            try
            {
                string location = GetLocation();
                if (File.Exists(location))
                {
                    string export = File.ReadAllText(location);
                    List<Tuple<DateTime, double, double, ulong, int, int>>? saved = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Tuple<DateTime, double, double, ulong, int, int>>>(export);
                    if (saved != null)
                    {
                        foreach (Tuple<DateTime, double, double, ulong, int, int> value in saved)
                        {
                            TimeSpan timestamp = value.Item1.TimeOfDay;
                            if (timestamp >= start && timestamp <= start + duration)
                            {
                                lock (_indexLock)
                                {
                                    _indexToModelMap[_index] = value;
                                    _index++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(LoadBar));
            }
        }

        private string GetLocation()
        {
            string directory = Path.Combine("./", "Bars");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string location = Path.Combine(directory, _security?.Symbol + ".json");
            return location;
        }

        private List<Tuple<DateTime, double, double, ulong, int, int>> GetUpdatesSince(ulong index, out ulong nextIndex)
        {
            List<Tuple<DateTime, double, double, ulong, int, int>> updates = new List<Tuple<DateTime, double, double, ulong, int, int>>();

            ulong count = _index - index;
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToModelMap.TryGetValue(index + i, out Tuple<DateTime, double, double, ulong, int, int>? update))
                    {
                        updates.Add(update);
                    }
                    else
                    {
                        _logger?.LogError(nameof(TryEncodeAndSend) + "Lookup failed for index: " + index + i);
                    }
                }
                nextIndex = index + count;
            }
            else
            {
                nextIndex = index;
            }

            return updates;
        }
    }
}

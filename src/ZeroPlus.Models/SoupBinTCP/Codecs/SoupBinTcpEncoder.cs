using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;

namespace ZeroPlus.Models.SoupBinTCP.Codecs
{
    public class SoupBinTcpEncoder : ISoupBinTcpEncoder
    {
        public const int DefaultMaxQueueCapacity = 5_000_000;
        private const int QueueWarningThreshold = 10_000;
        private const int QueueWarningInterval = 5_000;

        private readonly ILogger? _logger;
        private readonly IEncodeBufferContext _encodeContext;
        private readonly int _maxQueueCapacity;

        private bool _processMessages;
        private readonly ManualResetEventSlim _messageAddedNotifier;
        private Thread? _messageProcessingThread;
        private readonly object _queueLock;
        private readonly ConcurrentDictionary<ITopic, ulong> _topicToIndexMap;
        private readonly PriorityQueue<ITopic, MessagePriority> _messagesQueue;
        private readonly HashSet<ITopic> _queuedTopics;
        private int _warningLogCountdown;

        public IMessageSender? Sender { get; set; }

        public int MsgQueueCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _messagesQueue.Count;
                }
            }
        }

        public SoupBinTcpEncoder(IEncodeBufferContext encodeContext, int maxQueueCapacity = DefaultMaxQueueCapacity) : this(null!, encodeContext, maxQueueCapacity) { }

        public SoupBinTcpEncoder(ILogger<SoupBinTcpEncoder> logger, IEncodeBufferContext encodeContext, int maxQueueCapacity = DefaultMaxQueueCapacity) : this((ILogger)logger, encodeContext, maxQueueCapacity) { }

        public SoupBinTcpEncoder(ILogger? logger, IEncodeBufferContext encodeContext, int maxQueueCapacity = DefaultMaxQueueCapacity)
        {
            _logger = logger;
            _encodeContext = encodeContext;
            _maxQueueCapacity = maxQueueCapacity;
            _queueLock = new object();
            _messageAddedNotifier = new ManualResetEventSlim(false);
            _messagesQueue = new PriorityQueue<ITopic, MessagePriority>();
            _topicToIndexMap = new ConcurrentDictionary<ITopic, ulong>();
            _queuedTopics = new HashSet<ITopic>();
        }

        public void StartEngine()
        {
            StopEngine();
            _messageProcessingThread = new Thread(MessageProcessorThreadHandler)
            {
                IsBackground = true,
            };
            _processMessages = true;
            _messageProcessingThread.Start();
        }

        public void StopEngine()
        {
            if (_messageProcessingThread != null)
            {
                _processMessages = false;
                _messageAddedNotifier.Set();
                if (_messageProcessingThread.ThreadState == ThreadState.Running)
                {
                    _messageProcessingThread.Join();
                }
            }

            lock (_queueLock)
            {
                _queuedTopics.Clear();
            }
        }

        public void Send(ITopic? topic, bool sendCache)
        {
            if (topic == null)
            {
                return;
            }

            if (sendCache)
            {
                lock (_queueLock)
                {
                    if (_queuedTopics.Contains(topic))
                    {
                        return;
                    }

                    int count = _messagesQueue.Count;

                    if (count >= _maxQueueCapacity)
                    {
                        if (_warningLogCountdown <= 0)
                        {
                            _logger?.LogError("Encoder message queue full ({Count}/{Capacity}). Dropping message for topic {TopicId}.",
                                count, _maxQueueCapacity, topic.Id);
                            _warningLogCountdown = QueueWarningInterval;
                        }
                        else
                        {
                            _warningLogCountdown--;
                        }
                        return;
                    }

                    if (count >= QueueWarningThreshold && _warningLogCountdown <= 0)
                    {
                        _logger?.LogWarning("Encoder message queue depth high: {Count}/{Capacity}.", count, _maxQueueCapacity);
                        _warningLogCountdown = QueueWarningInterval;
                    }

                    _messagesQueue.Enqueue(topic, topic.MessagePriority);
                    _queuedTopics.Add(topic);
                }
                if (!_messageAddedNotifier.IsSet)
                {
                    _messageAddedNotifier.Set();
                }
            }
            else
            {
                _topicToIndexMap[topic] = topic.Index;
            }
        }

        public void Reset(ITopic? topic)
        {
            if (topic == null)
            {
                return;
            }
            _topicToIndexMap.TryRemove(topic, out _);
        }

        private void MessageProcessorThreadHandler()
        {
            bool success;
            ITopic topic;
            while (_processMessages)
            {
                try
                {
                    lock (_queueLock)
                    {
                        success = _messagesQueue.TryDequeue(out topic!, out _);
                        if (success)
                        {
                            _queuedTopics.Remove(topic);
                        }
                    }

                    if (success)
                    {
                        _topicToIndexMap.TryGetValue(topic, out ulong index);
                        var sender = Sender;
                        if (sender != null)
                        {
                            if (topic.TryEncodeAndSend(index, sender, _encodeContext, out ulong nextIndex))
                            {
                                _topicToIndexMap[topic] = nextIndex;
                            }
                        }
                    }
                    else
                    {
                        _messageAddedNotifier.Reset();
                        _messageAddedNotifier.Wait(1000);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, nameof(MessageProcessorThreadHandler));
                }
            }
        }
    }
}

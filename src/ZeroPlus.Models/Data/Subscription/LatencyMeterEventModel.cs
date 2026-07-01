namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct LatencyMeterEventModel
    {
        public readonly byte BoxId;
        public readonly byte ProgId;
        public readonly byte InstanceId;
        public readonly byte EventType;
        public readonly string? EventId;
        public readonly byte TimingSource;
        public readonly long TimestampNanos;

        public LatencyMeterEventModel(byte boxId, byte progId, byte instanceId,
            byte eventType, string? eventId, byte timingSource, long timestampNanos)
        {
            BoxId = boxId;
            ProgId = progId;
            InstanceId = instanceId;
            EventType = eventType;
            EventId = eventId;
            TimingSource = timingSource;
            TimestampNanos = timestampNanos;
        }
    }
}

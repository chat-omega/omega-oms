namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct StateSnapshotModel
    {
        public readonly byte BoxId;
        public readonly byte ProgId;
        public readonly byte InstanceId;
        public readonly string? SnapshotName;
        public readonly long TimestampNanos;
        public readonly StateSnapshotEntryModel[] Entries;

        public StateSnapshotModel(byte boxId, byte progId, byte instanceId,
            string? snapshotName, long timestampNanos, StateSnapshotEntryModel[] entries)
        {
            BoxId = boxId;
            ProgId = progId;
            InstanceId = instanceId;
            SnapshotName = snapshotName;
            TimestampNanos = timestampNanos;
            Entries = entries;
        }
    }
}

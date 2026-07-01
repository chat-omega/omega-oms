namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct StateSnapshotEntryModel
    {
        public readonly string Key;
        public readonly string Value;

        public StateSnapshotEntryModel(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}

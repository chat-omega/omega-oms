namespace ZeroPlus.Models.Data.Enums
{
    public enum ExecutionType
    {
        New = '0', // 0x30
        PartiallyFilled = '1', // 0x31
        Filled = '2', // 0x32
        DoneForDay = '3', // 0x33
        Canceled = '4', // 0x34
        Replaced = '5', // 0x35
        PendingCancel = '6', // 0x36
        Stopped = '7', // 0x37
        Rejected = '8', // 0x38
        Suspended = '9', // 0x39
        PendingNew = 'A', // 0x41
        Calculated = 'B', // 0x42
        Expired = 'C', // 0x43
        Restated = 'D', // 0x44
        PendingReplace = 'E', // 0x45
        Trade = 'F',  // (partial fill or fill)
        TradeCorrect = 'G',  // (formerly an ExecTransType)
        TradeCancel = 'H',  // (formerly an ExecTransType)
        OrderStatus = 'I',  // (formerly an ExecTransType)
        Released = 'N',

    }
}

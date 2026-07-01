namespace ZeroPlus.Models.SoupBinTCP.Data
{
    public enum RejectCode : byte
    {
        NotAuthorized = (byte)'A',
        SessionNotAvailable = (byte)'S',
    }
}

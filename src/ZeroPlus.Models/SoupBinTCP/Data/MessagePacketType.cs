namespace ZeroPlus.Models.SoupBinTCP.Data
{
    public enum MessagePacketType : byte
    {
        Debug = (byte)'+',
        LoginAccepted = (byte)'A',
        LoginRejected = (byte)'J',
        SequencedData = (byte)'S',
        UnSequencedData = (byte)'U',
        ServerHeartbeat = (byte)'H',
        EndOfSession = (byte)'Z',
        LoginRequest = (byte)'L',
        ClientHeartbeat = (byte)'R',
        LogoutRequest = (byte)'O',
        CompressedData = (byte)'C',
    }
}

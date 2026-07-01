using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class ServerHeartbeat : Message
    {
        private static readonly byte[] _bytes = Encoding.ASCII.GetBytes(['H']);

        public ServerHeartbeat()
        {
            Bytes = _bytes;
        }
    }
}
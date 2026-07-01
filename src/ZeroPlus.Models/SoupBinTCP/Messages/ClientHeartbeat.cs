using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class ClientHeartbeat : Message
    {
        private static readonly byte[] _bytes = Encoding.ASCII.GetBytes(['R']);
        public ClientHeartbeat()
        {
            Bytes = _bytes;
        }
    }
}
using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class EndOfSession : Message
    {
        private static readonly byte[] _bytes = Encoding.ASCII.GetBytes(['Z']);
        public EndOfSession()
        {
            Bytes = _bytes;
        }
    }
}
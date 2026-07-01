using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class LogoutRequest : Message
    {

        private static readonly byte[] _bytes = Encoding.ASCII.GetBytes(['O']);
        public LogoutRequest()
        {
            Bytes = _bytes;
        }
    }
}
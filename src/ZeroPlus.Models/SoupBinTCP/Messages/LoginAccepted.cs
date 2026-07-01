using System;
using System.Linq;
using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class LoginAccepted : Message
    {
        public string Session => Encoding.ASCII.GetString(Bytes.Skip(3).Take(10).ToArray());
        public ulong SequenceNumber => Convert.ToUInt64(Encoding.ASCII.GetString(Bytes.Skip(13).Take(20).ToArray()));

        public LoginAccepted()
        {
        }

        public LoginAccepted(string session, ulong sequenceNumber)
        {
            if (session.Length > 10)
            {
                throw new ArgumentOutOfRangeException(session, "Session must have maximum length 10");
            }

            const char type = 'A';

            session = session.PadLeft(10);
            string seqNo = sequenceNumber.ToString();
            seqNo = seqNo.PadLeft(20);
            string payload = type + session + seqNo;
            Bytes = Encoding.ASCII.GetBytes(payload);
        }
    }
}
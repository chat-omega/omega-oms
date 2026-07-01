using System;
using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class LoginRejected : Message
    {
        public char RejectReasonCode => Convert.ToChar(Bytes[1]);

        public LoginRejected()
        {
        }

        public LoginRejected(char rejectReasonCode)
        {
            if (rejectReasonCode != 'A' && rejectReasonCode != 'S')
            {
                throw new ArgumentException("Reject reason code must be either A or S", nameof(rejectReasonCode));
            }

            const char type = 'J';
            string payload = new(new[] { type, rejectReasonCode });
            Bytes = Encoding.ASCII.GetBytes(payload);
        }
    }
}
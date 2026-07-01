using System.Linq;
using System.Text;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class Debug : Message
    {
        public string Text => Encoding.ASCII.GetString(Bytes.Skip(1).Take(Length - 1).ToArray());

        public Debug()
        {
        }

        public Debug(string text)
        {
            const char type = '+';
            string payload = type + text;
            Bytes = Encoding.ASCII.GetBytes(payload);
        }
    }
}
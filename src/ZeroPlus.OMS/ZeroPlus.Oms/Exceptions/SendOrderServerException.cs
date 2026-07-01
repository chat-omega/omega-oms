using System;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Oms.Data.Models;

namespace ZeroPlus.Oms.Exceptions
{
    public class SendOrderServerException : Exception
    {
        public OpsOrderModel OmsOrder { get; set; }

        public SendOrderServerException(string message) : base(message)
        {
        }

        public override string ToString()
        {
            return $"{Message} -> Order: {OmsOrder?.ToString()}";
        }
    }
}

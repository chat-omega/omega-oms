using System;

namespace ZeroPlus.Oms.Exceptions
{
    public class BasketSubmissionCancelledException : ApplicationException
    {
        public override string StackTrace => "Operation Cancelled";
    }
}

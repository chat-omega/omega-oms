using System;

namespace ZeroPlus.Models.Exceptions
{
    public enum ErrorType
    {
        Generic,
        DuplicateOrderFound
    }

    public class SlimException : ApplicationException
    {
        public static SlimException Shared { get; } = new();

        public ErrorType ErrorType { get; set; }
        public override string StackTrace => "Slim Exception Trace";
        public SlimException() { }
        public SlimException(string message) : base(message) { }
        public SlimException(string message, ErrorType errorType) : base(message)
        {
            ErrorType = errorType;
        }
    }
}

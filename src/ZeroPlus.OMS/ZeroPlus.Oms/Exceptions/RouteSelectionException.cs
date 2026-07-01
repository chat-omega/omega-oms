using System;

namespace ZeroPlus.Oms.Exceptions
{
    public class RouteSelectionException : Exception
    {
        public string Route { get; set; }

        public RouteSelectionException(string message) : base(message) { }

        public override string ToString()
        {
            return $"{Message} -> Route: {Route}";
        }
    }
}

using System;

namespace ZeroPlus.Oms.Exceptions
{
    public class RouteSelectionMoveException : Exception
    {
        public string Route { get; set; }

        public RouteSelectionMoveException(string message) : base(message) { }

        public override string ToString()
        {
            return $"{Message} -> Route: {Route}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Oms.Config;

public class FishRoute
{
    public string Routes => string.Join(", ", RoutesList);
    public HashSet<string> RoutesList { get; set; } = new();
    public double Edge { get; set; }
    public double Increment { get; set; }
    public double Interval { get; set; }
    public bool Contains(string route)
    {
        return RoutesList != null && RoutesList.Contains(route, StringComparer.OrdinalIgnoreCase);
    }
}
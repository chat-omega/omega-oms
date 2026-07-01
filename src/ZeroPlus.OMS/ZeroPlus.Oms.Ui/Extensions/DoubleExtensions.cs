using System;

namespace ZeroPlus.Oms.Ui.Extensions;

public static class DoubleExtensions
{
    public static bool IsWhole(this double x)
    {
        return Math.Abs(x % 1) <= (double.Epsilon * 100);
    }
}
using System.ComponentModel;

namespace ZeroPlus.Models.Data.Enums.Contra
{
    /// <summary>
    /// FIX tag 9204. Each member's <see cref="DescriptionAttribute"/> carries the original FIX wire token.
    /// </summary>
    public enum ContraCapacity : byte
    {
        [Description("0")] _0 = 0,
        [Description("1")] _1 = 1,
        [Description("2")] _2 = 2,
        [Description("3")] _3 = 3,
        [Description("4")] _4 = 4,
        [Description("6")] _6 = 5,
        [Description("8")] _8 = 6,
        [Description("C")] C = 7,
        [Description("F")] F = 8,
        [Description("J")] J = 9,
        [Description("M")] M = 10,
        [Description("N")] N = 11,
        [Description("U")] U = 12,
    }
}

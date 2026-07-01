using System.ComponentModel;

namespace ZeroPlus.Models.Data.Enums.Contra
{
    /// <summary>
    /// FIX tag 9439. Each member's <see cref="DescriptionAttribute"/> carries the original FIX wire token.
    /// </summary>
    public enum ContraCmta : byte
    {
        [Description("10")] _10 = 0,
        [Description("161")] _161 = 1,
        [Description("164")] _164 = 2,
        [Description("17")] _17 = 3,
        [Description("200")] _200 = 4,
        [Description("255")] _255 = 5,
        [Description("333")] _333 = 6,
        [Description("352")] _352 = 7,
        [Description("365")] _365 = 8,
        [Description("431")] _431 = 9,
        [Description("5")] _5 = 10,
        [Description("50")] _50 = 11,
        [Description("541")] _541 = 12,
        [Description("551")] _551 = 13,
        [Description("598")] _598 = 14,
        [Description("642")] _642 = 15,
        [Description("67")] _67 = 16,
        [Description("68")] _68 = 17,
        [Description("695")] _695 = 18,
        [Description("733")] _733 = 19,
        [Description("792")] _792 = 20,
        [Description("813")] _813 = 21,
    }
}

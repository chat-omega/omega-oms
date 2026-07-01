namespace ZeroPlus.Models.Data.Enums.Matrix;

public enum MakeTake
{
    /// <summary>
    /// Take only - Only allow take orders.
    /// </summary>
    TakeOnly = 0,
    /// <summary>
    /// Take/Make - Allow both make and take orders
    /// </summary>
    TakeMake = 1,
    /// <summary>
    /// Make only - Sit a tick outside of the bid or offer to attempt to never take.
    /// </summary>
    MakeOnly = 2,
}
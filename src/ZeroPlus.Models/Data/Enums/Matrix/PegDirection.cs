namespace ZeroPlus.Models.Data.Enums.Matrix;

public enum PegDirection
{
    /// <summary>
    /// move peg price no matter price better or worse
    /// </summary>
    BOTH = 0,
    /// <summary>
    /// only move the pegged price when it worsens the current price 
    /// </summary>
    WORSE = 1,
    /// <summary>
    /// only move the pegged price when it betters the current price
    /// </summary>
    BETTER = 2,
}
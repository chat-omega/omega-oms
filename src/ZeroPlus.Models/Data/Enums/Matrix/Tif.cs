namespace ZeroPlus.Models.Data.Enums.Matrix;

public enum Tif
{
    /// <summary>
    /// MK_TIF_DAY
    /// Good for day.  National hours only.
    /// </summary>
    DAY = 0,

    /// <summary>
    /// MK_TIF_GTC
    /// Good till cancel.  Not currently supported.
    /// </summary>
    GTC = 1,

    /// <summary>
    /// MK_TIF_OPG
    /// At the open.
    /// </summary>
    OPG = 2,

    /// <summary>
    /// MK_TIF_IOC
    /// Immediate or cancel.
    /// </summary>
    IOC = 3,

    /// <summary>
    /// MK_TIF_FOK
    /// Fill or kill
    /// </summary>
    FOK = 4,

    /// <summary>
    /// MK_TIF_GTX
    /// Good till crossing.
    /// </summary>
    GTX = 5,

    /// <summary>
    /// MK_TIF_GTD
    /// Good till date.  Current day only.
    /// </summary>
    GTD = 6,

    /// <summary>
    /// MK_TIF_CLG
    /// At the close.  Market and limit orders only.
    /// </summary>
    CLG = 7,

    /// <summary>
    /// MK_TIF_AON
    /// All or none.
    /// </summary>
    AON = 8,
}
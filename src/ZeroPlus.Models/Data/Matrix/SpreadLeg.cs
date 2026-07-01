using System.ComponentModel.DataAnnotations;
using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix;

public class SpreadLeg
{
    [Required]
    [MaxLength(40)]
    public string? ClientGuid { get; set; }

    /// <summary>
    /// The leg ratio for a given spread contract
    /// </summary>
    [Required]
    public int LegRatio { get; set; }

    /// <summary>
    /// The leg side
    /// </summary>
    [Required]
    public Side Side { get; set; }

    /// <summary>
    /// The leg symbol.
    /// </summary>
    [Required]
    public string? Symbol { get; set; }

    /// <summary>
    /// The leg instrument type.
    /// </summary>
    [Required]
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// Optional open/close flag.
    /// If not present, we will set this flag for you based on your current position.
    /// </summary>
    public OpenClose? OpenClose { get; set; }

}
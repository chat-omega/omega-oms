using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models;

public record ForeignUpdateRouteKey(
    OrderSource OrderSource,
    OrderSubType SubType,
    string? Destination);

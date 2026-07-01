using System.Collections.Immutable;

namespace ZeroPlus.Models.Data.Models;

public record ForeignUpdateRoutesResponse(ImmutableArray<ForeignUpdateRouteRule> Rules);

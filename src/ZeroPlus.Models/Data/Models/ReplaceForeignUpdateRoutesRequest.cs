using System.Collections.Immutable;

namespace ZeroPlus.Models.Data.Models;

public record ReplaceForeignUpdateRoutesRequest(ImmutableArray<ForeignUpdateRouteRule> Rules);

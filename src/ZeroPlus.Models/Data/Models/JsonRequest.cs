using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public record JsonRequest
    {
        public int RequestId { get; init; }
        public RequestType RequestType { get; init; }
        public string? Content { get; init; }
    }
}

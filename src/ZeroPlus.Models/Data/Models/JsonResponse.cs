namespace ZeroPlus.Models.Data.Models
{
    public record JsonResponse
    {
        public int RequestId { get; init; }
        public bool IsSuccess { get; init; }
        public string? Content { get; init; }
    }
}

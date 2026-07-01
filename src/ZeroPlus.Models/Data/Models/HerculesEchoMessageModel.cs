using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Models;

public record HerculesEchoMessageModel(
    IOrder Order,
    string? Source,
    Venue Venue,
    int BookUpdateType
);

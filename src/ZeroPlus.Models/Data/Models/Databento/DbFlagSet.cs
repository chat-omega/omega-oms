using System;

namespace ZeroPlus.Models.Data.Models.Databento;

[Flags]
public enum DbFlagSet : byte
{
    None = 0,
    Reserved0 = 1,
    PublisherSpecific = 2,
    MaybeBadBook = 4,
    BadTsRecv = 8,
    Mbp = 16, // 0x10
    Snapshot = 32, // 0x20
    Tob = 64, // 0x40
    Last = 128, // 0x80
}
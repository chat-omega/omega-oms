using System;
using System.Runtime.InteropServices;

namespace ZeroPlus.Models.Data.SharedMemoryModels;

/// <summary>Explicit layout for Native AOT interop (<c>SharedArray&lt;T&gt;</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct RbboSharedMemoryModel
{
    public const int MaxSlots = 26;
    public const int SYMBOL_LEN = 30;

    public fixed byte SymbolBytes[SYMBOL_LEN];
    public int SymbolIndex;
    public int SlotCount;
    public uint KnownMcids;
    public uint ChangedMcids;
    public long SnapShotTime;

    public fixed byte Mcid[MaxSlots];
    public fixed byte Flags[MaxSlots];
    public fixed double BidPrice[MaxSlots];
    public fixed uint BidQty[MaxSlots];
    public fixed double AskPrice[MaxSlots];
    public fixed uint AskQty[MaxSlots];

    public string Symbol
    {
        set
        {
            int len = value.Length < SYMBOL_LEN ? value.Length : SYMBOL_LEN;
            for (int i = 0; i < len; i++)
            {
                SymbolBytes[i] = (byte)value[i];
            }
        }

        get
        {
            Span<char> buffer = stackalloc char[SYMBOL_LEN];
            int len = 0;
            for (; len < SYMBOL_LEN; len++)
            {
                if (SymbolBytes[len] == 0)
                    break;
                buffer[len] = (char)SymbolBytes[len];
            }
            return new string(buffer[..len]);
        }
    }
}

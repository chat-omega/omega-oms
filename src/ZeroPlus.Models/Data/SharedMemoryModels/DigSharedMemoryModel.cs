using System;
using System.Runtime.InteropServices;

namespace ZeroPlus.Models.Data.SharedMemoryModels;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DigSharedMemoryModel
{
    public const int SYMBOL_LEN = 30;

    public fixed byte SymbolBytes[SYMBOL_LEN];
    public int SymbolIndex;
    public double BidPrice;
    public double AskPrice;
    public int BidQty;
    public int AskQty;
    public long SnapShotTime;

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

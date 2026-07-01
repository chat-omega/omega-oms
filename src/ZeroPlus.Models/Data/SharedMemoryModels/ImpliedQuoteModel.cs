using System;
using System.Runtime.InteropServices;

namespace ZeroPlus.Models.Data.SharedMemoryModels;

/// <summary>Explicit layout for Native AOT interop (<c>SharedArray&lt;T&gt;</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ImpliedQuoteModel
{
    public const int SYMBOL_LEN = 30;

    public fixed byte SymbolBytes[SYMBOL_LEN];
    public double Bid;
    public double Ask;
    public double Theo;
    public double UnderBid;
    public double UnderAsk;
    public double ImpliedBid;
    public double ImpliedAsk;

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
using System;
using System.Runtime.InteropServices;

namespace ZeroPlus.Models.Data.SharedMemoryModels;

/// <summary>
/// Layout must match Raptor shared-memory writer. <see cref="StructLayoutAttribute"/> is required for Native AOT
/// (<c>SharedArray&lt;T&gt;</c> / interop size queries).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct DeltaAdjustedTheoModelPlus
{
    public const int SYMBOL_LEN = 30;

    public fixed byte SymbolBytes[SYMBOL_LEN];
    public double Delta;
    public double Theo;
    public double MidPrice;
    public double DeltaAdjustedTheo;
    public long SnapShotTime;
    public double SecondaryTheo;
    public double SecondaryTheoAdj;
    public double SecondaryIv;
    public double SecondaryUnder;
    public double SecondaryDelta;
    public double DaEma;
    public double VolaEma;

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
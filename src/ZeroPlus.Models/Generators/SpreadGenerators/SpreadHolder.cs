using System;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Generators.SpreadGenerators;

public class SpreadHolder
{
    private Option _leg0;
    private Option? _leg1;
    private Option? _leg2;
    private Option? _leg3;

    public Option this[int index]
    {
        get => index switch
        {
            0 => _leg0,
            1 => _leg1!,
            2 => _leg2!,
            3 => _leg3!,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0: _leg0 = value; break;
                case 1: _leg1 = value; break;
                case 2: _leg2 = value; break;
                case 3: _leg3 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public double Width { get; set; }

    public SpreadHolder(Option leg0)
    {
        _leg0 = leg0;
    }

    public SpreadHolder(Option leg0, Option leg1)
    {
        _leg0 = leg0;
        _leg1 = leg1;
    }

    public SpreadHolder(Option leg0, Option leg1, Option leg2)
    {
        _leg0 = leg0;
        _leg1 = leg1;
        _leg2 = leg2;
    }

    public SpreadHolder(Option leg0, Option leg1, Option leg2, Option leg3)
    {
        _leg0 = leg0;
        _leg1 = leg1;
        _leg2 = leg2;
        _leg3 = leg3;
    }
}

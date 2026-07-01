using System;
using System.Runtime.CompilerServices;
using System.Threading;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Generators;

/// <summary>
/// Generates fixed-width 20-character order IDs in uppercase alphanumeric (A-Z, 0-9).
/// Format: <c>SVA YYMMDD IIIII SSSSSS</c>
/// <list type="bullet">
///   <item><c>S</c> (1) — Source: A=AutoTrader, G=OrderGateway</item>
///   <item><c>V</c> (1) — Venue: O=OPS, T=TB, S=Silexx, Z=ZpFix, M=Matrix</item>
///   <item><c>A</c> (1) — Action: N=New, C=Cancel, R=Replace, P=Pair</item>
///   <item><c>YYMMDD</c> (6) — UTC date digits</item>
///   <item><c>IIIII</c> (5) — Process ID in base-36</item>
///   <item><c>SSSSSS</c> (6) — Sequence in base-36</item>
/// </list>
/// The only allocation per call is the returned 20-char <see cref="string"/>.
/// </summary>
public sealed class OrderIdGenerator
{
    public const int IdLength = 20;

    private const int DateOffset = 3;
    private const int DateWidth = 6;
    private const int InstanceOffset = 9;
    private const int InstanceWidth = 5;
    private const int SequenceOffset = 14;
    private const int SequenceWidth = 6;

    private static readonly char[] s_venueMap = BuildVenueMap();
    private static readonly char[] s_actionMap = BuildActionMap();

    private readonly char _source;
    private volatile string _dateStr;
    private readonly string _instanceStr;
    private long _nextMidnightTickCount;
    private uint _sequence;

    public OrderIdGenerator(char source, int processId)
    {
        _source = source;
        var now = DateTime.UtcNow;
        _dateStr = FormatDate(now);
        _nextMidnightTickCount = ComputeNextMidnightTickCount(now);
        _instanceStr = EncodeBase36((uint)processId, InstanceWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string NextId(Venue venue, OrderActionType action)
    {
        uint seq = Interlocked.Increment(ref _sequence);
        string date = _dateStr;

        if (Environment.TickCount64 >= _nextMidnightTickCount)
        {
            date = RefreshDate();
        }

        return string.Create(IdLength,
            (source: _source,
             date,
             instance: _instanceStr,
             venue: s_venueMap[(int)venue],
             action: s_actionMap[(int)action],
             seq),
            static (span, state) =>
            {
                span[0] = state.source;
                span[1] = state.venue;
                span[2] = state.action;
                state.date.AsSpan().CopyTo(span.Slice(DateOffset, DateWidth));
                state.instance.AsSpan().CopyTo(span.Slice(InstanceOffset, InstanceWidth));
                WriteBase36Unrolled(span.Slice(SequenceOffset, SequenceWidth), state.seq);
            });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string RefreshDate()
    {
        var now = DateTime.UtcNow;
        string date = FormatDate(now);
        _dateStr = date;
        Volatile.Write(ref _nextMidnightTickCount, ComputeNextMidnightTickCount(now));
        return date;
    }

    private static long ComputeNextMidnightTickCount(DateTime utcNow)
    {
        long msUntilMidnight = (long)(utcNow.Date.AddDays(1) - utcNow).TotalMilliseconds;
        return Environment.TickCount64 + msUntilMidnight;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBase36Unrolled(Span<char> span, uint value)
    {
        ReadOnlySpan<char> c = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        span[5] = c[(int)(value % 36)]; value /= 36;
        span[4] = c[(int)(value % 36)]; value /= 36;
        span[3] = c[(int)(value % 36)]; value /= 36;
        span[2] = c[(int)(value % 36)]; value /= 36;
        span[1] = c[(int)(value % 36)]; value /= 36;
        span[0] = c[(int)value];
    }

    private static string EncodeBase36(uint value, int width)
    {
        return string.Create(width, value, static (span, val) =>
        {
            ReadOnlySpan<char> c = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = span.Length - 1; i >= 0; i--)
            {
                span[i] = c[(int)(val % 36)];
                val /= 36;
            }
        });
    }

    private static string FormatDate(DateTime utcNow)
    {
        return string.Create(DateWidth, utcNow, static (span, dt) =>
        {
            int yy = dt.Year % 100;
            span[0] = (char)('0' + yy / 10);
            span[1] = (char)('0' + yy % 10);
            span[2] = (char)('0' + dt.Month / 10);
            span[3] = (char)('0' + dt.Month % 10);
            span[4] = (char)('0' + dt.Day / 10);
            span[5] = (char)('0' + dt.Day % 10);
        });
    }

    private static char[] BuildVenueMap()
    {
        var values = Enum.GetValues<Venue>();
        var map = new char[(int)values[^1] + 1];
        map[(int)Venue.OPS] = 'O';
        map[(int)Venue.TB] = 'T';
        map[(int)Venue.Silexx] = 'S';
        map[(int)Venue.ZpFix] = 'Z';
        map[(int)Venue.Matrix] = 'M';
        return map;
    }

    private static char[] BuildActionMap()
    {
        var values = Enum.GetValues<OrderActionType>();
        var map = new char[(int)values[^1] + 1];
        map[(int)OrderActionType.New] = 'N';
        map[(int)OrderActionType.Cancel] = 'C';
        map[(int)OrderActionType.Replace] = 'R';
        map[(int)OrderActionType.Pair] = 'P';
        return map;
    }
}

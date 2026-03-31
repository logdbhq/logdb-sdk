#if NETFRAMEWORK
// Polyfill for System.Index and System.Range to enable C# range/index syntax on .NET Framework
namespace System
{
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            _value = fromEnd ? ~value : value;
        }

        public static Index Start => new Index(0);
        public static Index End => new Index(~0);

        public static Index FromStart(int value) => new Index(value);
        public static Index FromEnd(int value) => new Index(~value);

        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => new Index(value);
    }

    internal readonly struct Range
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range StartAt(Index start) => new Range(start, Index.End);
        public static Range EndAt(Index end) => new Range(Index.Start, end);
        public static Range All => new Range(Index.Start, Index.End);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            return (start, end - start);
        }
    }
}

internal static class RangeExtensions
{
    public static string Substring(this string s, System.Range range)
    {
        var (offset, length) = range.GetOffsetAndLength(s.Length);
        return s.Substring(offset, length);
    }
}
#endif

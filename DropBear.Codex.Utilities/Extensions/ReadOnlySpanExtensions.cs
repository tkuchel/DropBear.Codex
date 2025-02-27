#region

using System.Runtime.InteropServices;

#endregion

namespace DropBear.Codex.Utilities.Extensions;

public static class ReadOnlySpanExtensions
{
    public static ZipEnumerator Zip(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        return new ZipEnumerator(first, second);
    }

    [StructLayout(LayoutKind.Auto)]
    public ref struct ZipEnumerator
    {
        private readonly ReadOnlySpan<byte> _first;
        private readonly ReadOnlySpan<byte> _second;
        private int _index;

        public ZipEnumerator(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            _first = first;
            _second = second;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _first.Length && _index < _second.Length;
        }

        public (byte First, byte Second) Current
        {
            get
            {
                if (_index < 0 || _index >= _first.Length || _index >= _second.Length)
                {
                    throw new InvalidOperationException("Enumerator is in an invalid state.");
                }

                return (_first[_index], _second[_index]);
            }
        }

        public ZipEnumerator GetEnumerator()
        {
            return this;
        }
    }
}

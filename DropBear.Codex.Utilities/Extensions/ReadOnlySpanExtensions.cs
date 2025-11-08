#region

using System.Runtime.InteropServices;

#endregion

namespace DropBear.Codex.Utilities.Extensions;

/// <summary>
///     Provides extension methods for <see cref="ReadOnlySpan{T}"/> operations with zero-allocation enumerators.
/// </summary>
public static class ReadOnlySpanExtensions
{
    /// <summary>
    ///     Zips two byte spans together, creating a zero-allocation enumerator over paired elements.
    /// </summary>
    /// <param name="first">The first byte span to zip.</param>
    /// <param name="second">The second byte span to zip.</param>
    /// <returns>
    ///     A <see cref="ZipEnumerator"/> that can be used in foreach loops to iterate over paired bytes
    ///     from both spans. Enumeration stops when the shorter span is exhausted.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///     This method provides a zero-allocation alternative to LINQ's Zip for byte spans.
    ///     The returned enumerator is a ref struct and cannot escape the stack, ensuring maximum performance.
    ///     </para>
    ///     <para><strong>Performance:</strong> No heap allocations, ideal for hot paths and high-throughput scenarios.</para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     ReadOnlySpan&lt;byte&gt; data1 = stackalloc byte[] { 1, 2, 3 };
    ///     ReadOnlySpan&lt;byte&gt; data2 = stackalloc byte[] { 10, 20, 30 };
    ///
    ///     foreach (var (first, second) in data1.Zip(data2))
    ///     {
    ///         Console.WriteLine($"{first}, {second}"); // Outputs: "1, 10", "2, 20", "3, 30"
    ///     }
    ///     </code>
    /// </example>
    public static ZipEnumerator Zip(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        return new ZipEnumerator(first, second);
    }

    /// <summary>
    ///     A zero-allocation enumerator that zips two byte spans together.
    ///     This is a ref struct that cannot escape the stack, ensuring optimal performance.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Ref structs are stack-only types that cannot be boxed or stored on the heap.
    ///     This provides maximum performance by eliminating allocations entirely.
    ///     </para>
    ///     <para>
    ///     The <see cref="StructLayoutAttribute"/> with <see cref="LayoutKind.Auto"/> allows the runtime to optimize
    ///     the struct layout for best performance.
    ///     </para>
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public ref struct ZipEnumerator
    {
        private readonly ReadOnlySpan<byte> _first;
        private readonly ReadOnlySpan<byte> _second;
        private int _index;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ZipEnumerator"/> struct.
        /// </summary>
        /// <param name="first">The first byte span to enumerate.</param>
        /// <param name="second">The second byte span to enumerate.</param>
        public ZipEnumerator(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            _first = first;
            _second = second;
            _index = -1;
        }

        /// <summary>
        ///     Advances the enumerator to the next pair of elements from both spans.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the enumerator successfully advanced to the next pair;
        ///     <c>false</c> if the enumerator has passed the end of either span.
        /// </returns>
        /// <remarks>
        ///     Enumeration stops when the shorter span is exhausted, ensuring both spans
        ///     always have a valid element at the current index.
        /// </remarks>
        public bool MoveNext()
        {
            _index++;
            return _index < _first.Length && _index < _second.Length;
        }

        /// <summary>
        ///     Gets the current pair of bytes from both spans at the current position.
        /// </summary>
        /// <value>
        ///     A tuple containing the current byte from the first span and the current byte from the second span.
        /// </value>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the enumerator is positioned before the first element (index -1) or
        ///     after the last element of either span.
        /// </exception>
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

        /// <summary>
        ///     Returns the enumerator itself to support foreach iteration.
        /// </summary>
        /// <returns>This enumerator instance.</returns>
        public ZipEnumerator GetEnumerator()
        {
            return this;
        }
    }
}

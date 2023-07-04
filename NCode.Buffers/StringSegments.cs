#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Buffers;
using System.Diagnostics;

namespace NCode.Buffers;

/// <summary>
/// Provides the ability to split a string into substrings based on a delimiter without any additional heap allocations.
/// </summary>
public class StringSegments
{
    /// <summary>
    /// Gets the original string.
    /// </summary>
    public ReadOnlyMemory<char> Original { get; }

    /// <summary>
    /// Gets the number of substrings.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the first substring.
    /// </summary>
    public ReadOnlySequenceSegment<char> First { get; }

    /// <summary>
    /// Gets the substring at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the substring to get.</param>
    public ReadOnlySequenceSegment<char> this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            var iter = First;
            while (index > 0 && iter != null)
            {
                iter = iter.Next;
                --index;
            }

            Debug.Assert(iter != null);
            return iter;
        }
    }

    private StringSegments(ReadOnlyMemory<char> original, int count, ReadOnlySequenceSegment<char> first)
    {
        Original = original;
        Count = count;
        First = first;
    }

    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">A character that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(string original, char separator)
        => Split(original.AsMemory(), separator);

    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">A character that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(ReadOnlyMemory<char> original, char separator)
    {
        var count = 1;
        var index = original.Span.IndexOf(separator);
        if (index == -1)
        {
            return new StringSegments(original, count, new MemorySegment<char>(original));
        }

        var first = new MemorySegment<char>(original[..index]);
        var last = first;
        var offset = index + 1;

        while (true)
        {
            ++count;

            index = original.Span[offset..].IndexOf(separator);
            if (index == -1)
            {
                last.Append(original[offset..]);
                return new StringSegments(original, count, first);
            }

            index += offset;
            last = last.Append(original.Slice(offset, index - offset));
            offset = index + 1;
        }
    }

    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(string original, ReadOnlySpan<char> separator)
        => Split(original.AsMemory(), separator, StringComparison.Ordinal);

    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <param name="comparisonType">An enumeration that specifies the rules for the substring search.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(string original, ReadOnlySpan<char> separator, StringComparison comparisonType)
        => Split(original.AsMemory(), separator, comparisonType);


    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(ReadOnlyMemory<char> original, ReadOnlySpan<char> separator) =>
        Split(original, separator, StringComparison.Ordinal);

    /// <summary>
    /// Splits a string into substrings based on a delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="original">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <param name="comparisonType">An enumeration that specifies the rules for the substring search.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited by the separator.</returns>
    public static StringSegments Split(ReadOnlyMemory<char> original, ReadOnlySpan<char> separator, StringComparison comparisonType)
    {
        var count = 1;
        var index = original.Span.IndexOf(separator, comparisonType);
        if (index == -1)
        {
            return new StringSegments(original, count, new MemorySegment<char>(original));
        }

        var first = new MemorySegment<char>(original[..index]);
        var last = first;
        var offset = index + separator.Length;

        while (true)
        {
            ++count;

            index = original.Span[offset..].IndexOf(separator, comparisonType);
            if (index == -1)
            {
                last.Append(original[offset..]);
                return new StringSegments(original, count, first);
            }

            last = last.Append(original.Slice(offset, index - offset));
            offset = index + separator.Length;
        }
    }
}

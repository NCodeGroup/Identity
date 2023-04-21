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

namespace NIdentity.OpenId.Buffers;

/// <summary>
/// Provides the ability to split a string into substrings based on a delimiting character without any additional heap allocations.
/// </summary>
public class StringSplitSequenceSegment : ReadOnlySequenceSegment<char>
{
    private StringSplitSequenceSegment()
    {
        // nothing
    }

    /// <summary>
    /// Splits a string into substrings based on a specified delimiting character without any additional heap allocations.
    /// </summary>
    /// <param name="value">The string to split into substrings.</param>
    /// <param name="separator">A character that delimits the substrings in this string.</param>
    /// <param name="count">When this method returns, contains the number of substrings.</param>
    /// <returns>An <see cref="ReadOnlySequenceSegment{T}"/> that contains the substrings from the string that are delimited by separator.</returns>
    public static ReadOnlySequenceSegment<char> Split(string value, char separator, out int count)
    {
        var memory = value.AsMemory();
        var index = value.IndexOf(separator);

        if (index == -1)
        {
            count = 1;
            return new StringSplitSequenceSegment
            {
                Memory = memory
            };
        }

        var first = new StringSplitSequenceSegment
        {
            Memory = memory[..index]
        };

        var counter = 2;
        var last = first;
        var lastIndex = index + 1;

        while (true)
        {
            index = value.IndexOf(separator, lastIndex);
            if (index == -1)
            {
                index = memory.Length;
            }

            var length = index - lastIndex;
            var next = new StringSplitSequenceSegment
            {
                Memory = memory.Slice(lastIndex, length),
                RunningIndex = last.RunningIndex + last.Memory.Length
            };

            last.Next = next;

            if (index + 1 > memory.Length)
            {
                count = counter;
                return first;
            }

            ++counter;
            last = next;
            lastIndex = index + 1;
        }
    }
}

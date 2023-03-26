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
using System.Buffers.Binary;

namespace NIdentity.OpenId.Cryptography;

internal static class BinaryUtility
{
    public const int BitsPerByte = 8;

    public static void Xor(ReadOnlySpan<byte> xBuffer, long y, Span<byte> destination)
    {
        if (xBuffer.Length < sizeof(long))
            throw new InvalidOperationException();
        if (destination.Length < sizeof(long))
            throw new InvalidOperationException();

        var x = BinaryPrimitives.ReadInt64BigEndian(xBuffer);

        var result = x ^ y;

        BinaryPrimitives.WriteInt64BigEndian(destination, result);
    }

    public static ReadOnlySequence<byte> Concat(IEnumerable<ReadOnlyMemory<byte>> buffers)
    {
        MemorySegment<byte>? first = null;
        MemorySegment<byte>? last = null;

        foreach (var buffer in buffers)
        {
            if (first == null)
            {
                first = new MemorySegment<byte>(buffer);
            }
            else
            {
                last = (last ?? first).Append(buffer);
            }
        }

        if (first == null)
            return ReadOnlySequence<byte>.Empty;

        if (last == null)
            return new ReadOnlySequence<byte>(first.Memory);

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    public static ReadOnlySequence<byte> Concat(ReadOnlySpan<byte> prepend, IEnumerable<ReadOnlyMemory<byte>> buffers)
    {
        var first = new MemorySegment<byte>(prepend.ToArray());
        var last = buffers.Aggregate(first, (segment, buffer) => segment.Append(buffer));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    public static ReadOnlySequence<byte> Concat(params ReadOnlyMemory<byte>[] buffers)
    {
        switch (buffers.Length)
        {
            case 0:
                return ReadOnlySequence<byte>.Empty;
            case 1:
                return new ReadOnlySequence<byte>(buffers[0]);
            default:
                var first = new MemorySegment<byte>(buffers[0]);
                var last = buffers.Skip(1).Aggregate(first, (segment, buffer) => segment.Append(buffer));
                return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }
    }

    public static void Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> destination)
    {
        if (a.Length + b.Length > destination.Length)
            throw new InvalidOperationException();

        a.CopyTo(destination);
        b.CopyTo(destination[a.Length..]);
    }
}

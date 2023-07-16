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
using System.Diagnostics;
using System.Text;
using NIdentity.OpenId.Logic;

namespace NIdentity.OpenId.Cryptography.Binary;

// TODO: move
internal enum BinaryStringEncoding
{
    Ascii = 0,
    Utf8,
    Base64,
    Base64Url
}

internal class KdfBinaryWriter : BinaryWriter
{
    // we only allow ASCII
    private static Encoding Encoding { get; } = Encoding.ASCII;

    public KdfBinaryWriter(Stream output)
        : base(output, Encoding)
    {
        // nothing
    }

    /*
      The AlgorithmID value is of the form Datalen || Data, where Data
      is a variable-length string of zero or more octets, and Datalen is
      a fixed-length, big-endian 32-bit counter that indicates the
      length (in octets) of Data.  In the Direct Key Agreement case,
      Data is set to the octets of the ASCII representation of the "enc"
      Header Parameter value.  In the Key Agreement with Key Wrapping
      case, Data is set to the octets of the ASCII representation of the
      "alg" (algorithm) Header Parameter value.
     */

    public void Write(string? value, BinaryStringEncoding binaryEncoding)
    {
        if (string.IsNullOrEmpty(value))
        {
            Write(0);
            return;
        }

        int byteCount;
        Encoding? encoding = null;
        switch (binaryEncoding)
        {
            case BinaryStringEncoding.Ascii:
                encoding = Encoding.ASCII;
                byteCount = encoding.GetByteCount(value);
                break;

            case BinaryStringEncoding.Utf8:
                encoding = Encoding.UTF8;
                byteCount = encoding.GetByteCount(value);
                break;

            case BinaryStringEncoding.Base64:
                // TODO
                throw new NotImplementedException();

            case BinaryStringEncoding.Base64Url:
                byteCount = Base64Url.GetByteCountForDecode(value.Length);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(binaryEncoding), binaryEncoding, null);
        }

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var actualByteCount = 0;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (binaryEncoding)
            {
                case BinaryStringEncoding.Ascii:
                case BinaryStringEncoding.Utf8:
                    actualByteCount = encoding!.GetBytes(value, rented);
                    break;

                case BinaryStringEncoding.Base64:
                {
                    var result = Convert.TryFromBase64String(value, rented, out actualByteCount);
                    Debug.Assert(result);
                    break;
                }

                case BinaryStringEncoding.Base64Url:
                {
                    var result = Base64Url.TryDecode(value.AsSpan(), rented, out actualByteCount);
                    Debug.Assert(result);
                    break;
                }
            }

            Write(actualByteCount);
            Write(rented, 0, actualByteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void WriteBase64Url(string? value)
        => Write(value, BinaryStringEncoding.Base64Url);

    /// <inheritdoc />
    public override void Write(string value) =>
        Write(value, BinaryStringEncoding.Ascii);

    /// <inheritdoc />
    public override void Write(float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(Half value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteHalfBigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        OutStream.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        OutStream.Write(buffer);
    }
}

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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NCode.Encoders;

namespace NIdentity.OpenId.Logic;

public interface ICryptoService
{
    void GenerateBytes(Span<byte> destination);

    string EncodeBinary(ReadOnlySpan<byte> data, BinaryEncodingType binaryEncodingType);

    string GenerateKey(int byteLength, BinaryEncodingType binaryEncodingType);

    string HashValue(ReadOnlySpan<byte> data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType);

    string HashValue(string data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType);
}

internal class CryptoService : ICryptoService
{
    private const int MaxStackAlloc = 512 >> 3;

    public void GenerateBytes(Span<byte> destination) =>
        RandomNumberGenerator.Fill(destination);

    public string EncodeBinary(ReadOnlySpan<byte> data, BinaryEncodingType binaryEncodingType) =>
        binaryEncodingType switch
        {
            BinaryEncodingType.Base64Url => Base64Url.Encode(data),
            BinaryEncodingType.Base64 => Convert.ToBase64String(data),
            BinaryEncodingType.Hex => Convert.ToHexString(data),
            _ => throw new ArgumentException("Unsupported encoding", nameof(binaryEncodingType))
        };

    public string GenerateKey(int byteLength, BinaryEncodingType binaryEncodingType)
    {
        var bytes = byteLength <= MaxStackAlloc ?
            stackalloc byte[byteLength] :
            GC.AllocateUninitializedArray<byte>(byteLength, pinned: false);

        GenerateBytes(bytes);

        return EncodeBinary(bytes, binaryEncodingType);
    }

    private static HashAlgorithm CreateHashAlgorithm(HashAlgorithmType hashAlgorithmType) =>
        hashAlgorithmType switch
        {
            HashAlgorithmType.Sha1 => SHA1.Create(),
            HashAlgorithmType.Sha256 => SHA256.Create(),
            _ => throw new ArgumentException("Unsupported algorithm", nameof(hashAlgorithmType))
        };

    public string HashValue(ReadOnlySpan<byte> data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        using var hashAlgorithm = CreateHashAlgorithm(hashAlgorithmType);

        var hashByteLength = (hashAlgorithm.HashSize + 7) >> 3;
        var hashBytes = hashByteLength <= MaxStackAlloc ?
            stackalloc byte[hashByteLength] :
            GC.AllocateUninitializedArray<byte>(hashByteLength, pinned: false);

        var result = hashAlgorithm.TryComputeHash(data, hashBytes, out var bytesWritten);
        Debug.Assert(result && bytesWritten == hashByteLength);

        return EncodeBinary(hashBytes, binaryEncodingType);
    }

    public string HashValue(string data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        var dataByteLength = Encoding.UTF8.GetByteCount(data);
        var dataBytes = dataByteLength <= MaxStackAlloc ?
            stackalloc byte[dataByteLength] :
            GC.AllocateUninitializedArray<byte>(dataByteLength, pinned: false);

        var bytesWritten = Encoding.UTF8.GetBytes(data, dataBytes);
        Debug.Assert(bytesWritten == dataByteLength);

        return HashValue(dataBytes, hashAlgorithmType, binaryEncodingType);
    }
}

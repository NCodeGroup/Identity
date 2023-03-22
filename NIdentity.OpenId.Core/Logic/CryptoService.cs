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

using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Logic;

public interface ICryptoService
{
    byte[] GenerateBytes(int byteLength);

    string EncodeBinary(byte[] bytes, BinaryEncodingType binaryEncodingType);

    string GenerateKey(int byteLength, BinaryEncodingType binaryEncodingType);

    string HashValue(byte[] bytes, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType);

    string HashValue(string value, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType);
}

internal class CryptoService : ICryptoService, IDisposable
{
    private RandomNumberGenerator RandomNumberGenerator { get; } = RandomNumberGenerator.Create();

    public void Dispose()
    {
        RandomNumberGenerator.Dispose();
    }

    public byte[] GenerateBytes(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.GetBytes(bytes);
        return bytes;
    }

    public string EncodeBinary(byte[] bytes, BinaryEncodingType binaryEncodingType)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return binaryEncodingType switch
        {
            BinaryEncodingType.Base64Url => Base64UrlEncoder.Encode(bytes),
            BinaryEncodingType.Base64 => Convert.ToBase64String(bytes),
            BinaryEncodingType.Hex => Convert.ToHexString(bytes),
            _ => throw new ArgumentException("Unsupported encoding", nameof(binaryEncodingType))
        };
    }

    public string GenerateKey(int byteLength, BinaryEncodingType binaryEncodingType)
    {
        var bytes = GenerateBytes(byteLength);
        return EncodeBinary(bytes, binaryEncodingType);
    }

    private static HashAlgorithm CreateHashAlgorithm(HashAlgorithmType hashAlgorithmType)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return hashAlgorithmType switch
        {
            HashAlgorithmType.Sha256 => SHA256.Create(),
            _ => throw new ArgumentException("Unsupported algorithm", nameof(hashAlgorithmType))
        };
    }

    public string HashValue(byte[] bytes, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        using var hashAlgorithm = CreateHashAlgorithm(hashAlgorithmType);
        var hashBytes = hashAlgorithm.ComputeHash(bytes);
        return EncodeBinary(hashBytes, binaryEncodingType);
    }

    public string HashValue(string value, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        using var hashAlgorithm = CreateHashAlgorithm(hashAlgorithmType);
        var hashBytes = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
        return EncodeBinary(hashBytes, binaryEncodingType);
    }
}

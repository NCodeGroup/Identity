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
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

/// <summary>
/// Base class for all crypto factories that use <see cref="SymmetricSecretKey"/> as their secret key.
/// </summary>
/// <typeparam name="TCryptoFactory">The concrete <see cref="ICryptoFactory"/> type.</typeparam>
public abstract class SymmetricCryptoFactory<TCryptoFactory> : CryptoFactory<TCryptoFactory>
    where TCryptoFactory : CryptoFactory<TCryptoFactory>, new()
{
    /// <inheritdoc />
    public override Type SecretKeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    protected override KeyMaterial GenerateKeyMaterial(int keySizeBits)
    {
        var byteLength = keySizeBits / BinaryUtility.BitsPerByte;
        var byteLease = CryptoPool.Rent(byteLength);
        try
        {
            RandomNumberGenerator.Fill(byteLease.Memory.Span);
            return new SymmetricKeyMaterial(byteLease);
        }
        catch
        {
            byteLease.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override SecretKey CreateSecretKey(string keyId, int keySizeBits, ReadOnlySpan<byte> keyBytes) =>
        new SymmetricSecretKey(keyId, keyBytes);
}

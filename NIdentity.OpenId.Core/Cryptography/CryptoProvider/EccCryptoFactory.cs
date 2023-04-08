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
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

/// <summary>
/// Base class for all crypto factories that use <see cref="EccSecretKey"/> as their secret key.
/// </summary>
/// <typeparam name="TCryptoFactory">The concrete <see cref="ICryptoFactory"/> type.</typeparam>
public abstract class EccCryptoFactory<TCryptoFactory> : CryptoFactory<TCryptoFactory>
    where TCryptoFactory : CryptoFactory<TCryptoFactory>, new()
{
    /// <inheritdoc />
    public override Type SecretKeyType => typeof(EccSecretKey);

    /// <inheritdoc />
    protected override KeyMaterial GenerateKeyMaterial(int keyBitLength) =>
        keyBitLength switch
        {
            256 => new EccKeyMaterial(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)),
            384 => new EccKeyMaterial(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384)),
            521 => new EccKeyMaterial(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521)),
            _ => throw new InvalidOperationException()
        };

    /// <inheritdoc />
    protected override SecretKey CreateSecretKey(string keyId, int keyBitLength, ReadOnlySpan<byte> keyMaterial) =>
        new EccSecretKey(keyId, keyBitLength, keyMaterial);
}

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
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Cryptography.archive;

// ECDH-ES-*
// ECDiffieHellman

internal class ECDiffieHellmanSecurityKey : AsymmetricSecurityKey
{
    private bool HasPrivateKeyD { get; }

    public ECDiffieHellman ECDiffieHellman { get; }

    public override int KeySize => ECDiffieHellman.KeySize;

    [Obsolete("HasPrivateKey method is deprecated, please use PrivateKeyStatus instead.")]
    public override bool HasPrivateKey => HasPrivateKeyD;

    public override PrivateKeyStatus PrivateKeyStatus => HasPrivateKeyD ? PrivateKeyStatus.Exists : PrivateKeyStatus.DoesNotExist;

    public ECDiffieHellmanSecurityKey(ECDiffieHellman ecDiffieHellman, bool hasPrivateKeyD)
    {
        ECDiffieHellman = ecDiffieHellman;
        HasPrivateKeyD = hasPrivateKeyD;
    }
}

internal class ECDiffieHellmanKeyWrapProvider : KeyWrapProvider
{
    private ECDiffieHellmanSecurityKey ECDiffieHellmanSecurityKey { get; }

    private int CekSizeBits { get; }

    public override string? Context { get; set; }

    public override string Algorithm { get; }

    public override SecurityKey Key => ECDiffieHellmanSecurityKey;

    public ECDiffieHellmanKeyWrapProvider(string algorithm, ECDiffieHellmanSecurityKey key, int cekSizeBits)
    {
        Algorithm = algorithm;
        ECDiffieHellmanSecurityKey = key;
        CekSizeBits = cekSizeBits;
    }

    protected override void Dispose(bool disposing)
    {
        // nothing
    }

    public override byte[] WrapKey(byte[] keyBytes)
    {
        const bool includePrivateParameters = false;
        var parameters = ECDiffieHellmanSecurityKey.ECDiffieHellman.ExportParameters(includePrivateParameters);

        using var otherPartyKey = ECDiffieHellman.Create(parameters.Curve);
        otherPartyKey.ImportSubjectPublicKeyInfo(keyBytes, out _);

        var secretPrepend = Array.Empty<byte>();
        var secretAppend = Array.Empty<byte>();

        using var otherPartyPublicKey = otherPartyKey.PublicKey;
        var derivedKey = ECDiffieHellmanSecurityKey.ECDiffieHellman.DeriveKeyFromHash(
            otherPartyPublicKey,
            HashAlgorithmName.SHA256,
            secretPrepend,
            secretAppend);

        var cekSizeBytes = CekSizeBits / 8;
        if (derivedKey.Length > cekSizeBytes)
            return derivedKey.AsSpan(0, cekSizeBytes).ToArray();

        return derivedKey;
    }

    public override byte[] UnwrapKey(byte[] keyBytes)
    {
        throw new NotImplementedException();
    }
}

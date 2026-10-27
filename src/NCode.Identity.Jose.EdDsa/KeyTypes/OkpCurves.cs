#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using Ed25519Math = NCode.Identity.Jose.EdDsa.Math.Ed25519;
using Ed448Math = NCode.Identity.Jose.EdDsa.Math.Ed448;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Catalog of the supported OKP signing curves (Ed25519, Ed448) and the dispatch to their clean-room
/// implementations. Identified by the JWK <c>crv</c> name (RFC 8037) or the X.509 algorithm OID (RFC 8410).
/// </summary>
internal static class OkpCurves
{
    public const string Ed25519Name = "Ed25519";
    public const string Ed448Name = "Ed448";

    private static readonly OkpCurveInfo Ed25519Info = new(Ed25519Name, "1.3.101.112", 32, 32, 64);
    private static readonly OkpCurveInfo Ed448Info = new(Ed448Name, "1.3.101.113", 57, 57, 114);

    // The Ed448 ID-token hash is SHAKE256 with a 114-octet output (OIDF Connect issue #1125).
    private const int Ed448TokenHashSizeBytes = 114;

    public static OkpCurveInfo Ed25519 => Ed25519Info;
    public static OkpCurveInfo Ed448 => Ed448Info;

    public static int Ed25519KeySizeBits => Ed25519Info.KeySizeBits;
    public static int Ed448KeySizeBits => Ed448Info.KeySizeBits;

    public static int MaxPublicKeySize => Ed448Info.PublicKeySize;
    public static int MaxSeedSize => Ed448Info.SeedSize;
    public static int MaxSignatureSize => Ed448Info.SignatureSize;

    public static bool TryGetByName(string? curve, [NotNullWhen(true)] out OkpCurveInfo? info)
    {
        info = curve switch
        {
            Ed25519Name => Ed25519Info,
            Ed448Name => Ed448Info,
            _ => null,
        };
        return info is not null;
    }

    public static bool TryGetByKeySizeBits(
        int keySizeBits,
        [NotNullWhen(true)] out OkpCurveInfo? info
    )
    {
        info =
            keySizeBits == Ed25519Info.KeySizeBits ? Ed25519Info
            : keySizeBits == Ed448Info.KeySizeBits ? Ed448Info
            : null;
        return info is not null;
    }

    public static bool TryGetByOid(string? oid, [NotNullWhen(true)] out OkpCurveInfo? info)
    {
        info = oid switch
        {
            "1.3.101.112" => Ed25519Info,
            "1.3.101.113" => Ed448Info,
            _ => null,
        };
        return info is not null;
    }

    public static void GeneratePublicKey(
        string curve,
        ReadOnlySpan<byte> seed,
        Span<byte> publicKey
    )
    {
        if (curve == Ed25519Name)
        {
            Ed25519Math.GeneratePublicKey(seed, publicKey);
        }
        else
        {
            Ed448Math.GeneratePublicKey(seed, publicKey);
        }
    }

    public static void Sign(
        string curve,
        ReadOnlySpan<byte> seed,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    )
    {
        if (curve == Ed25519Name)
        {
            Ed25519Math.Sign(seed, message, signature);
        }
        else
        {
            Ed448Math.Sign(seed, message, signature);
        }
    }

    public static bool Verify(
        string curve,
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    ) =>
        curve == Ed25519Name
            ? Ed25519Math.Verify(publicKey, message, signature)
            : Ed448Math.Verify(publicKey, message, signature);

    public static HashFunctionDelegate GetTokenHashFunction(
        OkpCurveInfo curve,
        out int hashSizeBytes
    )
    {
        if (curve.Name == Ed25519Name)
        {
            hashSizeBytes = 64;
            return SHA512.TryHashData;
        }

        hashSizeBytes = Ed448TokenHashSizeBytes;
        return TryShake256TokenHash;
    }

    private static bool TryShake256TokenHash(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        out int bytesWritten
    )
    {
        if (destination.Length < Ed448TokenHashSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        Shake256.HashData(source, destination[..Ed448TokenHashSizeBytes]);
        bytesWritten = Ed448TokenHashSizeBytes;
        return true;
    }
}

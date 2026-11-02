// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using Ed25519Math = NCode.Identity.Jose.EdDsa.Math.Ed25519;
using Ed448Math = NCode.Identity.Jose.EdDsa.Math.Ed448;
using X25519Math = NCode.Identity.Jose.EdDsa.Math.X25519;
using X448Math = NCode.Identity.Jose.EdDsa.Math.X448;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Catalog of the supported OKP curves — the signing curves Ed25519 and Ed448 and the key-agreement curves X25519 and
/// X448 — and the dispatch to their clean-room implementations. Identified by the JWK <c>crv</c> name (RFC 8037) or
/// the X.509 algorithm OID (RFC 8410).
/// </summary>
// See ADR-1390.
internal static class OkpCurves
{
    public const string Ed25519Name = "Ed25519";
    public const string Ed448Name = "Ed448";
    public const string X25519Name = "X25519";
    public const string X448Name = "X448";

    private static readonly OkpCurveInfo Ed25519Info = new(
        Ed25519Name,
        "1.3.101.112",
        OkpCurveUse.Signature,
        PrivateKeySize: 32,
        PublicKeySize: 32,
        SignatureSize: 64
    );

    private static readonly OkpCurveInfo Ed448Info = new(
        Ed448Name,
        "1.3.101.113",
        OkpCurveUse.Signature,
        PrivateKeySize: 57,
        PublicKeySize: 57,
        SignatureSize: 114
    );

    private static readonly OkpCurveInfo X25519Info = new(
        X25519Name,
        "1.3.101.110",
        OkpCurveUse.KeyAgreement,
        PrivateKeySize: X25519Math.PrivateKeySize,
        PublicKeySize: X25519Math.PublicKeySize,
        SignatureSize: 0
    );

    private static readonly OkpCurveInfo X448Info = new(
        X448Name,
        "1.3.101.111",
        OkpCurveUse.KeyAgreement,
        PrivateKeySize: X448Math.PrivateKeySize,
        PublicKeySize: X448Math.PublicKeySize,
        SignatureSize: 0
    );

    // The Ed448 ID-token hash is SHAKE256 with a 114-octet output (OIDF Connect issue #1125).
    private const int Ed448TokenHashSizeBytes = 114;

    public static OkpCurveInfo Ed25519 => Ed25519Info;
    public static OkpCurveInfo Ed448 => Ed448Info;
    public static OkpCurveInfo X25519 => X25519Info;
    public static OkpCurveInfo X448 => X448Info;

    public static int Ed25519KeySizeBits => Ed25519Info.KeySizeBits;
    public static int Ed448KeySizeBits => Ed448Info.KeySizeBits;
    public static int X25519KeySizeBits => X25519Info.KeySizeBits;
    public static int X448KeySizeBits => X448Info.KeySizeBits;

    /// <summary>
    /// Gets the names of every supported curve, for messages that list them.
    /// </summary>
    public static string SupportedNames { get; } =
        $"'{Ed25519Name}', '{Ed448Name}', '{X25519Name}' or '{X448Name}'";

    /// <summary>
    /// Resolves a curve of any use by its JWK <c>crv</c> name.
    /// </summary>
    public static bool TryGetByName(string? curve, [NotNullWhen(true)] out OkpCurveInfo? info)
    {
        info = curve switch
        {
            Ed25519Name => Ed25519Info,
            Ed448Name => Ed448Info,
            X25519Name => X25519Info,
            X448Name => X448Info,
            _ => null,
        };
        return info is not null;
    }

    /// <summary>
    /// Resolves a curve of the specified use by its JWK <c>crv</c> name, so a key of the other use is never resolved.
    /// </summary>
    public static bool TryGetByName(
        string? curve,
        OkpCurveUse use,
        [NotNullWhen(true)] out OkpCurveInfo? info
    )
    {
        if (TryGetByName(curve, out info) && info.Use == use)
        {
            return true;
        }

        info = null;
        return false;
    }

    /// <summary>
    /// Resolves a signing curve by its key size; the size alone cannot tell Ed25519 from X25519, so only signing
    /// curves are considered.
    /// </summary>
    public static bool TryGetSigningCurveByKeySizeBits(
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

    /// <summary>
    /// Resolves a curve of any use by its X.509 algorithm OID (RFC 8410 §3).
    /// </summary>
    public static bool TryGetByOid(string? oid, [NotNullWhen(true)] out OkpCurveInfo? info)
    {
        info = oid switch
        {
            "1.3.101.110" => X25519Info,
            "1.3.101.111" => X448Info,
            "1.3.101.112" => Ed25519Info,
            "1.3.101.113" => Ed448Info,
            _ => null,
        };
        return info is not null;
    }

    /// <summary>
    /// Derives the public key of the curve from its private key: the encoded point <c>[s]B</c> of an Edwards curve,
    /// or the u-coordinate <c>X25519(k, 9)</c> / <c>X448(k, 5)</c> of a Montgomery curve.
    /// </summary>
    public static void GeneratePublicKey(
        OkpCurveInfo curve,
        ReadOnlySpan<byte> privateKey,
        Span<byte> publicKey
    )
    {
        switch (curve.Name)
        {
            case Ed25519Name:
                Ed25519Math.GeneratePublicKey(privateKey, publicKey);
                break;
            case Ed448Name:
                Ed448Math.GeneratePublicKey(privateKey, publicKey);
                break;
            case X25519Name:
                X25519Math.GeneratePublicKey(privateKey, publicKey);
                break;
            case X448Name:
                X448Math.GeneratePublicKey(privateKey, publicKey);
                break;
            default:
                throw new InvalidOperationException(
                    $"The OKP curve '{curve.Name}' is not supported."
                );
        }
    }

    /// <summary>
    /// Signs with a signing curve; a key-agreement curve throws.
    /// </summary>
    public static void Sign(
        OkpCurveInfo curve,
        ReadOnlySpan<byte> seed,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    )
    {
        switch (curve.Name)
        {
            case Ed25519Name:
                Ed25519Math.Sign(seed, message, signature);
                break;
            case Ed448Name:
                Ed448Math.Sign(seed, message, signature);
                break;
            default:
                throw new InvalidOperationException($"The OKP curve '{curve.Name}' cannot sign.");
        }
    }

    /// <summary>
    /// Verifies with a signing curve; a key-agreement curve throws.
    /// </summary>
    public static bool Verify(
        OkpCurveInfo curve,
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    ) =>
        curve.Name switch
        {
            Ed25519Name => Ed25519Math.Verify(publicKey, message, signature),
            Ed448Name => Ed448Math.Verify(publicKey, message, signature),
            _ => throw new InvalidOperationException(
                $"The OKP curve '{curve.Name}' cannot verify signatures."
            ),
        };

    /// <summary>
    /// Agrees a shared secret with a key-agreement curve, writing <see cref="OkpCurveInfo.PublicKeySize"/> bytes.
    /// Returns <c>false</c> when the shared secret is all zeros (RFC 7748 §6); a signing curve throws.
    /// </summary>
    public static bool TryAgree(
        OkpCurveInfo curve,
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret
    ) =>
        curve.Name switch
        {
            X25519Name => X25519Math.TryAgree(privateKey, peerPublicKey, sharedSecret),
            X448Name => X448Math.TryAgree(privateKey, peerPublicKey, sharedSecret),
            _ => throw new InvalidOperationException(
                $"The OKP curve '{curve.Name}' cannot agree keys."
            ),
        };

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

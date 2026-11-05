// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Secp256k1.KeyTypes;

/// <summary>
/// The secp256k1 elliptic curve (SEC 2 §2.4.1, OID <c>1.3.132.0.10</c>) that RFC 8812 registers as the <c>JWK</c>
/// <c>crv</c> value <c>secp256k1</c>, and whether the platform's cryptography supports it.
/// </summary>
// See ADR-1392.
internal static class Secp256k1Curve
{
    /// <summary>
    /// The curve's <c>JWK</c> <c>crv</c> name (RFC 8812 §3.1).
    /// </summary>
    public const string Name = "secp256k1";

    /// <summary>
    /// The curve's object identifier (SEC 2 §2.4.1).
    /// </summary>
    public const string Oid = "1.3.132.0.10";

    /// <summary>
    /// The size of the curve, in bits.
    /// </summary>
    public const int SizeBits = 256;

    private static Lazy<bool> LazyIsSupported { get; } =
        new(ProbeSupport, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the curve's specification for the elliptic-curve catalog.
    /// </summary>
    public static EccCurveSpecification Specification { get; } =
        new()
        {
            CurveName = Name,
            CurveSizeBits = SizeBits,
            Curve = ECCurve.CreateFromValue(Oid),
            Domain = EccCurveDomain.FromHex(
                "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F",
                "00",
                "07"
            ),
        };

    /// <summary>
    /// Gets a value indicating whether the platform's cryptography can create, sign with and verify with a secp256k1
    /// key. The OpenSSL that .NET uses on Linux supports the curve; the platform cryptography on macOS does not.
    /// </summary>
    public static bool IsSupported => LazyIsSupported.Value;

    private static bool ProbeSupport()
    {
        try
        {
            using var key = ECDsa.Create(Specification.Curve);

            ReadOnlySpan<byte> data = [0x01];
            Span<byte> signature = stackalloc byte[2 * (SizeBits / 8)];
            return key.TrySignData(data, signature, HashAlgorithmName.SHA256, out var bytesWritten)
                && key.VerifyData(data, signature[..bytesWritten], HashAlgorithmName.SHA256);
        }
        catch (Exception exception)
            when (exception is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

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

using System.Security.Cryptography;

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// PureEdDSA signing and verification over Curve25519 (Ed25519), per RFC 8032 §5.1 and the JOSE <c>EdDSA</c> algorithm
/// (RFC 8037). Hashing uses the platform SHA-512; the curve, scalar, and group arithmetic are the clean-room
/// constant-time primitives in this package. Signing is constant-time with respect to the private key; verification
/// operates on public values only.
/// </summary>
internal static class Ed25519
{
    public const int SeedSize = 32;
    public const int PublicKeySize = 32;
    public const int SignatureSize = 64;

    private const int HashSize = 64;

    /// <summary>Derives the 32-byte public key (encoded point <c>A = [s]B</c>) from the 32-byte private seed.</summary>
    public static void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        Span<byte> h = stackalloc byte[HashSize];
        Span<byte> s = stackalloc byte[32];
        try
        {
            SHA512.HashData(privateKey, h);
            h[..32].CopyTo(s);
            Clamp(s);
            Ge25519.ScalarMultBase(s).ToBytes(publicKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(h);
            CryptographicOperations.ZeroMemory(s);
        }
    }

    /// <summary>Produces the 64-byte signature <c>R || S</c> over <paramref name="message"/> using the private seed.</summary>
    public static void Sign(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    )
    {
        Span<byte> h = stackalloc byte[HashSize];
        Span<byte> s = stackalloc byte[32];
        Span<byte> rHash = stackalloc byte[HashSize];
        Span<byte> r = stackalloc byte[32];
        Span<byte> kHash = stackalloc byte[HashSize];
        Span<byte> k = stackalloc byte[32];
        Span<byte> a = stackalloc byte[PublicKeySize];
        Span<byte> rPoint = stackalloc byte[32];
        try
        {
            SHA512.HashData(privateKey, h);
            h[..32].CopyTo(s);
            Clamp(s);
            var prefix = h[32..64];

            Ge25519.ScalarMultBase(s).ToBytes(a);

            // r = SHA-512(prefix || M) mod L
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512))
            {
                hash.AppendData(prefix);
                hash.AppendData(message);
                hash.GetHashAndReset(rHash);
            }

            Sc25519.Reduce(rHash, r);
            Ge25519.ScalarMultBase(r).ToBytes(rPoint);

            // k = SHA-512(R || A || M) mod L
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512))
            {
                hash.AppendData(rPoint);
                hash.AppendData(a);
                hash.AppendData(message);
                hash.GetHashAndReset(kHash);
            }

            Sc25519.Reduce(kHash, k);

            // S = (k * s + r) mod L
            rPoint.CopyTo(signature[..32]);
            Sc25519.MulAdd(k, s, r, signature.Slice(32, 32));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(h);
            CryptographicOperations.ZeroMemory(s);
            CryptographicOperations.ZeroMemory(rHash);
            CryptographicOperations.ZeroMemory(r);
            CryptographicOperations.ZeroMemory(kHash);
            CryptographicOperations.ZeroMemory(k);
        }
    }

    /// <summary>Verifies the 64-byte signature against the 32-byte public key and message.</summary>
    public static bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    )
    {
        if (
            signature.Length != SignatureSize
            || publicKey.Length != PublicKeySize
            || !Sc25519.IsCanonical(signature.Slice(32, 32))
            || !Ge25519.TryFromBytes(publicKey, out var a)
        )
        {
            return false;
        }

        var rBytes = signature[..32];

        // k = SHA-512(R || A || M) mod L
        Span<byte> kHash = stackalloc byte[HashSize];
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512))
        {
            hash.AppendData(rBytes);
            hash.AppendData(publicKey);
            hash.AppendData(message);
            hash.GetHashAndReset(kHash);
        }

        Span<byte> k = stackalloc byte[32];
        Sc25519.Reduce(kHash, k);

        // Check the cofactorless group equation [S]B = R + [k]A by recomputing [S]B - [k]A and comparing to R.
        var sB = Ge25519.ScalarMultBase(signature.Slice(32, 32));
        var kA = Ge25519.ScalarMult(k, a);
        var check = Ge25519.Add(sB, Ge25519.Neg(kA));

        Span<byte> checkBytes = stackalloc byte[32];
        check.ToBytes(checkBytes);
        return CryptographicOperations.FixedTimeEquals(checkBytes, rBytes);
    }

    // RFC 8032 §5.1.5: clear the low three bits of the first octet, clear the high bit and set the second-high bit
    // of the last octet, pinning the scalar into the correct range and cofactor class.
    private static void Clamp(Span<byte> scalar)
    {
        scalar[0] &= 0xF8;
        scalar[31] &= 0x7F;
        scalar[31] |= 0x40;
    }
}

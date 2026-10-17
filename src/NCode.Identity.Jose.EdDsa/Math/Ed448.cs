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
/// PureEdDSA signing and verification over edwards448 (Ed448), per RFC 8032 §5.2 and the JOSE <c>EdDSA</c> algorithm
/// (RFC 8037). Hashing uses the platform SHAKE256 (114-byte output) with the <c>dom4</c> prefix; the curve, scalar,
/// and group arithmetic are the clean-room constant-time primitives in this package. Signing is constant-time with
/// respect to the private key; verification operates on public values only.
/// </summary>
internal static class Ed448
{
    public const int SeedSize = 57;
    public const int PublicKeySize = 57;
    public const int SignatureSize = 114;

    private const int HashSize = 114;

    // dom4(0, "") = "SigEd448" || octet(phflag=0) || octet(context-length=0).
    private static ReadOnlySpan<byte> Dom4 =>
        [0x53, 0x69, 0x67, 0x45, 0x64, 0x34, 0x34, 0x38, 0x00, 0x00];

    /// <summary>Derives the 57-byte public key (encoded point <c>A = [s]B</c>) from the 57-byte private seed.</summary>
    public static void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        Span<byte> h = stackalloc byte[HashSize];
        Span<byte> s = stackalloc byte[SeedSize];
        try
        {
            Shake256.HashData(privateKey, h);
            h[..SeedSize].CopyTo(s);
            Clamp(s);
            Ge448.ScalarMultBase(s).ToBytes(publicKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(h);
            CryptographicOperations.ZeroMemory(s);
        }
    }

    /// <summary>Produces the 114-byte signature <c>R || S</c> over <paramref name="message"/> using the private seed.</summary>
    public static void Sign(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    )
    {
        Span<byte> h = stackalloc byte[HashSize];
        Span<byte> s = stackalloc byte[SeedSize];
        Span<byte> rHash = stackalloc byte[HashSize];
        Span<byte> r = stackalloc byte[SeedSize];
        Span<byte> kHash = stackalloc byte[HashSize];
        Span<byte> k = stackalloc byte[SeedSize];
        Span<byte> a = stackalloc byte[PublicKeySize];
        Span<byte> rPoint = stackalloc byte[PublicKeySize];
        try
        {
            Shake256.HashData(privateKey, h);
            h[..SeedSize].CopyTo(s);
            Clamp(s);
            var prefix = h[SeedSize..HashSize];

            Ge448.ScalarMultBase(s).ToBytes(a);

            // r = SHAKE256(dom4 || prefix || M, 114) mod L
            using (var shake = new Shake256())
            {
                shake.AppendData(Dom4);
                shake.AppendData(prefix);
                shake.AppendData(message);
                shake.GetHashAndReset(rHash);
            }

            Sc448.Reduce(rHash, r);
            Ge448.ScalarMultBase(r).ToBytes(rPoint);

            // k = SHAKE256(dom4 || R || A || M, 114) mod L
            using (var shake = new Shake256())
            {
                shake.AppendData(Dom4);
                shake.AppendData(rPoint);
                shake.AppendData(a);
                shake.AppendData(message);
                shake.GetHashAndReset(kHash);
            }

            Sc448.Reduce(kHash, k);

            rPoint.CopyTo(signature[..SeedSize]);
            Sc448.MulAdd(k, s, r, signature.Slice(SeedSize, SeedSize));
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

    /// <summary>Verifies the 114-byte signature against the 57-byte public key and message.</summary>
    public static bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    )
    {
        if (
            signature.Length != SignatureSize
            || publicKey.Length != PublicKeySize
            || !Sc448.IsCanonical(signature.Slice(SeedSize, SeedSize))
            || !Ge448.TryFromBytes(publicKey, out var a)
        )
        {
            return false;
        }

        var rBytes = signature[..SeedSize];

        Span<byte> kHash = stackalloc byte[HashSize];
        using (var shake = new Shake256())
        {
            shake.AppendData(Dom4);
            shake.AppendData(rBytes);
            shake.AppendData(publicKey);
            shake.AppendData(message);
            shake.GetHashAndReset(kHash);
        }

        Span<byte> k = stackalloc byte[SeedSize];
        Sc448.Reduce(kHash, k);

        var sB = Ge448.ScalarMultBase(signature.Slice(SeedSize, SeedSize));
        var kA = Ge448.ScalarMult(k, a);
        var check = Ge448.Add(sB, Ge448.Neg(kA));

        Span<byte> checkBytes = stackalloc byte[PublicKeySize];
        check.ToBytes(checkBytes);
        return CryptographicOperations.FixedTimeEquals(checkBytes, rBytes);
    }

    // RFC 8032 §5.2.5: clear the low two bits of the first octet, clear the last octet, set the high bit of the
    // second-to-last octet.
    private static void Clamp(Span<byte> scalar)
    {
        scalar[0] &= 0xFC;
        scalar[55] |= 0x80;
        scalar[56] = 0x00;
    }
}

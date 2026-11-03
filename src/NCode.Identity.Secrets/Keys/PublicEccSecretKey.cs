// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="EccSecretKey"/> that holds only the <b>public</b> half of an elliptic-curve key (as a
/// <c>SubjectPublicKeyInfo</c>). It can verify <c>ECDSA</c> signatures but not produce them or derive shared secrets;
/// it is used for keys a relying party registers for verification (for example a client's <c>private_key_jwt</c> / JAR
/// key resolved from its JWKS). The public key is not secret, so it is stored unprotected.
/// </summary>
internal sealed class PublicEccSecretKey(
    KeyMetadata metadata,
    string? curveName,
    int curveSizeBits,
    ReadOnlyMemory<byte> subjectPublicKeyInfo,
    ReadOnlyMemory<byte>? certificateRawData
) : EccSecretKey(metadata, curveName, curveSizeBits, certificateRawData)
{
    private ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    /// <inheritdoc />
    public override ECDsa ExportECDsa()
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override ECDiffieHellman ExportECDiffieHellman()
    {
        var ecDiffieHellman = ECDiffieHellman.Create();
        try
        {
            ecDiffieHellman.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return ecDiffieHellman;
        }
        catch
        {
            ecDiffieHellman.Dispose();
            throw;
        }
    }
}

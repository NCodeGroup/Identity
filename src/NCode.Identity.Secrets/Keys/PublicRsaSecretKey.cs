// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="RsaSecretKey"/> that holds only the <b>public</b> half of an RSA key (as a
/// <c>SubjectPublicKeyInfo</c>). It can verify signatures but not produce them; it is used for keys a relying party
/// registers for verification (for example a client's <c>private_key_jwt</c> / JAR key resolved from its JWKS). The
/// public key is not secret, so it is stored unprotected.
/// </summary>
internal sealed class PublicRsaSecretKey(
    KeyMetadata metadata,
    int modulusSizeBits,
    ReadOnlyMemory<byte> subjectPublicKeyInfo,
    ReadOnlyMemory<byte>? certificateRawData
) : RsaSecretKey(metadata, modulusSizeBits, certificateRawData)
{
    private ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    /// <inheritdoc />
    public override RSA ExportRSA()
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}

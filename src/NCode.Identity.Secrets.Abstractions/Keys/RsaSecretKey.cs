// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Represents an <see cref="AsymmetricSecretKey"/> implementation using <c>RSA</c> cryptographic keys.
/// </summary>
[PublicAPI]
public abstract class RsaSecretKey(
    KeyMetadata metadata,
    int modulusSizeBits,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : AsymmetricSecretKey(metadata, modulusSizeBits, certificateRawData, issuerCertificatesRawData)
{
    /// <summary>
    /// OID for <c>RSA</c> public key cryptography.
    /// </summary>
    public const string Oid = "1.2.840.113549.1.1.1";

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Rsa;

    /// <summary>
    /// Factory method to create an <see cref="RSA"/> instance from the current <c>RSA</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="RSA"/> instance</returns>
    public abstract RSA ExportRSA();

    /// <inheritdoc />
    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        using var key = ExportRSA();
        WriteSubjectPublicKeyInfo(key, ref destination);
    }
}

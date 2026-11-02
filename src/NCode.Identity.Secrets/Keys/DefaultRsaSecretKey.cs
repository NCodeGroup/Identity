// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides a default implementation of the <see cref="RsaSecretKey"/> abstraction that holds the protected
/// <c>PKCS#8</c> private key.
/// </summary>
internal class DefaultRsaSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    int modulusSizeBits,
    ReadOnlyMemory<byte> protectedPkcs8PrivateKey,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : RsaSecretKey(metadata, modulusSizeBits, certificateRawData, issuerCertificatesRawData)
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedPkcs8PrivateKey { get; } = protectedPkcs8PrivateKey;

    /// <inheritdoc />
    public override RSA ExportRSA() =>
        DataProtector.ExportAsymmetricAlgorithm(ProtectedPkcs8PrivateKey.Span, RSA.Create);
}

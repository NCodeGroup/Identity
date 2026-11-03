// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides a default implementation of the <see cref="EccSecretKey"/> abstraction that holds the protected
/// <c>PKCS#8</c> private key.
/// </summary>
/// <remarks>
/// Can be used for either <see cref="ECDsa"/> or <see cref="ECDiffieHellman"/> keys.
/// </remarks>
internal class DefaultEccSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    string? curveName,
    int curveSizeBits,
    ReadOnlyMemory<byte> protectedPkcs8PrivateKey,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : EccSecretKey(metadata, curveName, curveSizeBits, certificateRawData, issuerCertificatesRawData)
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedPkcs8PrivateKey { get; } = protectedPkcs8PrivateKey;

    /// <inheritdoc />
    public override ECDsa ExportECDsa() =>
        DataProtector.ExportAsymmetricAlgorithm(ProtectedPkcs8PrivateKey.Span, ECDsa.Create);

    /// <inheritdoc />
    public override ECDiffieHellman ExportECDiffieHellman() =>
        DataProtector.ExportAsymmetricAlgorithm(
            ProtectedPkcs8PrivateKey.Span,
            ECDiffieHellman.Create
        );
}

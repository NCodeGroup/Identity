#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Common abstractions for all secret keys using <c>asymmetric</c> cryptographic keys. Holds the shared
/// <see cref="SecretKey.Metadata"/>, <see cref="SecretKey.KeySizeBits"/>, and optional public certificate, so
/// concrete keys only supply their key-material export.
/// </summary>
[PublicAPI]
public abstract class AsymmetricSecretKey(
    KeyMetadata metadata,
    int keySizeBits,
    ReadOnlyMemory<byte>? certificateRawData
) : SecretKey
{
    /// <summary>
    /// Gets the raw DER-encoded bytes of the optional public certificate, or <see langword="null"/> when this
    /// asymmetric key has no associated certificate. Consuming the bytes directly (for example to compute a
    /// certificate thumbprint) avoids materializing an <see cref="X509Certificate2"/> via
    /// <see cref="ExportCertificate"/>.
    /// </summary>
    public ReadOnlyMemory<byte>? CertificateRawData { get; } = certificateRawData;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits { get; } = keySizeBits;

    /// <summary>
    /// Gets a value indicating whether this asymmetric key has an associated certificate.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CertificateRawData))]
    public bool HasCertificate => CertificateRawData.HasValue;

    /// <summary>
    /// Gets the optional <see cref="X509Certificate2"/> for this asymmetric key.
    /// </summary>
    /// <remarks>
    /// This certificate only contains the public portion.
    /// To create a certificate with both the private and public portions, use <c>CopyWithPrivateKey</c>.
    /// Doing so will create certificates with ephemeral keys and not persist keys to disk.
    /// </remarks>
    public X509Certificate2? ExportCertificate() =>
        HasCertificate
            ? X509CertificateLoader.LoadCertificate(CertificateRawData.Value.Span)
            : null;

    /// <summary>
    /// Factory method to create an <see cref="AsymmetricAlgorithm"/> instance from the current key material.
    /// </summary>
    public abstract AsymmetricAlgorithm ExportAsymmetricAlgorithm();

    /// <summary>
    /// Exports the <b>public</b> half of this asymmetric key as a <c>SubjectPublicKeyInfo</c> <c>PEM</c>
    /// (<c>-----BEGIN PUBLIC KEY-----</c>). No private key material is included.
    /// </summary>
    public virtual string ExportSubjectPublicKeyInfoPem()
    {
        using var algorithm = ExportAsymmetricAlgorithm();
        return algorithm.ExportSubjectPublicKeyInfoPem();
    }
}

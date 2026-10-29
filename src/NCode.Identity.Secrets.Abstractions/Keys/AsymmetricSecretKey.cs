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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Common abstractions for all secret keys using <c>asymmetric</c> cryptographic keys. Holds the shared
/// <see cref="SecretKey.Metadata"/>, <see cref="SecretKey.KeySizeBits"/>, the optional public certificate, and the
/// certificates of the authorities that issued it, so concrete keys only supply their key-material export.
/// </summary>
/// <param name="metadata">The metadata of the key.</param>
/// <param name="keySizeBits">The size of the key, in bits.</param>
/// <param name="certificateRawData">The optional DER-encoded certificate of the key, or <see langword="null"/>.</param>
/// <param name="issuerCertificatesRawData">The DER-encoded certificates of the authorities that issued the key's
/// certificate, such as an intermediate CA's, or <see langword="null"/> for none.</param>
[PublicAPI]
public abstract class AsymmetricSecretKey(
    KeyMetadata metadata,
    int keySizeBits,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : SecretKey
{
    /// <summary>
    /// Gets the DER-encoded certificates of the authorities that issued <see cref="CertificateRawData"/>, such as an
    /// intermediate CA's, or an empty list. A TLS client presents them with its certificate, so a server that trusts
    /// only the root can build the chain (RFC 8446 §4.4.2). They are public; no private key material is included.
    /// </summary>
    // See ADR-0300.
    public IReadOnlyList<ReadOnlyMemory<byte>> IssuerCertificatesRawData { get; } =
        issuerCertificatesRawData ?? [];

    /// <summary>
    /// Gets the certificates of the authorities that issued this key's certificate, each with only its public
    /// portion. The caller owns, and disposes, the returned certificates.
    /// </summary>
    /// <returns>The issuer certificates, empty when there are none.</returns>
    public X509Certificate2Collection ExportIssuerCertificates()
    {
        var collection = new X509Certificate2Collection();
        foreach (var rawData in IssuerCertificatesRawData)
        {
            collection.Add(X509CertificateLoader.LoadCertificate(rawData.Span));
        }

        return collection;
    }

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
    /// Writes the <b>public</b> half of this asymmetric key as <c>SubjectPublicKeyInfo</c> (SPKI) DER to
    /// <paramref name="destination"/>. This is the universal public-key-material primitive for every asymmetric
    /// family: classical keys backed by an <see cref="AsymmetricAlgorithm"/> (RSA/ECC) and families whose keys are
    /// <b>not</b> an <see cref="AsymmetricAlgorithm"/> instance (for example post-quantum ML-DSA / SLH-DSA, or
    /// Edwards-curve OKP keys), which expose their key material only as SPKI / PKCS#8 DER. No private key material
    /// is included.
    /// </summary>
    /// <param name="destination">The destination buffer writer that receives the SPKI DER bytes.</param>
    public abstract void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct;

    /// <summary>
    /// Exports the <b>public</b> half of this asymmetric key as a <c>SubjectPublicKeyInfo</c> <c>PEM</c>
    /// (<c>-----BEGIN PUBLIC KEY-----</c>). No private key material is included.
    /// </summary>
    public virtual string ExportSubjectPublicKeyInfoPem()
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        IBufferWriter<byte> writer = buffer;
        ExportSubjectPublicKeyInfo(ref writer);
        using var lease = buffer.GetSpanLease(isSensitive: false);
        return PemEncoding.WriteString("PUBLIC KEY", lease.Span);
    }

    /// <summary>
    /// Writes the <c>SubjectPublicKeyInfo</c> (SPKI) DER of the specified <paramref name="algorithm"/> to
    /// <paramref name="destination"/>, growing the request until the export succeeds.
    /// </summary>
    private protected static void WriteSubjectPublicKeyInfo<TWriter>(
        AsymmetricAlgorithm algorithm,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var sizeHint = 512;
        while (true)
        {
            var span = destination.GetSpan(sizeHint);
            if (algorithm.TryExportSubjectPublicKeyInfo(span, out var bytesWritten))
            {
                destination.Advance(bytesWritten);
                return;
            }

            sizeHint = checked(sizeHint * 2);
        }
    }
}

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
using NCode.Jose.DataProtection;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation of the <see cref="RsaSecretKey"/> abstraction.
/// </summary>
public class DefaultRsaSecretKey(
    ISecureDataProtector dataProtector,
    KeyMetadata metadata,
    int modulusSizeBits,
    byte[] protectedPkcs8PrivateKey,
    byte[]? certificateRawData
) : RsaSecretKey
{
    private ISecureDataProtector DataProtector { get; } = dataProtector;
    private byte[] ProtectedPkcs8PrivateKey { get; } = protectedPkcs8PrivateKey;
    private byte[]? CertificateRawData { get; } = certificateRawData;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits { get; } = modulusSizeBits;

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(CertificateRawData))]
    public override bool HasCertificate => CertificateRawData is not null;

    /// <inheritdoc />
    public override X509Certificate2? ExportCertificate() =>
        HasCertificate ? new X509Certificate2(CertificateRawData) : null;

    /// <inheritdoc />
    public override RSA ExportRSA() =>
        DataProtector.ExportAsymmetricAlgorithm(
            ProtectedPkcs8PrivateKey,
            RSA.Create,
            out _);
}

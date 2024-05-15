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
using NCode.Identity.DataProtection;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides a default implementation of the <see cref="EccSecretKey"/> abstraction.
/// </summary>
/// <remarks>
/// Can be used for either <see cref="ECDsa"/> or <see cref="ECDiffieHellman"/> keys.
/// </remarks>
public class DefaultEccSecretKey(
    ISecureDataProtector dataProtector,
    KeyMetadata metadata,
    int curveSizeBits,
    byte[] protectedPkcs8PrivateKey,
    byte[]? certificateRawData
) : EccSecretKey
{
    private ISecureDataProtector DataProtector { get; } = dataProtector;
    private byte[] ProtectedPkcs8PrivateKey { get; } = protectedPkcs8PrivateKey;
    private byte[]? CertificateRawData { get; } = certificateRawData;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits { get; } = curveSizeBits;

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(CertificateRawData))]
    public override bool HasCertificate => CertificateRawData is not null;

    /// <inheritdoc />
    public override X509Certificate2? ExportCertificate() =>
        HasCertificate ? new X509Certificate2(CertificateRawData) : null;

    /// <inheritdoc />
    public override ECCurve GetECCurve() => KeySizeBits switch
    {
        256 => ECCurve.NamedCurves.nistP256,
        384 => ECCurve.NamedCurves.nistP384,
        521 => ECCurve.NamedCurves.nistP521,
        _ => GetECCurveSlow()
    };

    private ECCurve GetECCurveSlow()
    {
        using var key = ExportECDiffieHellman();
        var parameters = key.ExportParameters(includePrivateParameters: false);
        return parameters.Curve;
    }

    /// <inheritdoc />
    public override ECDsa ExportECDsa() =>
        DataProtector.ExportAsymmetricAlgorithm(
            ProtectedPkcs8PrivateKey,
            ECDsa.Create);

    /// <inheritdoc />
    public override ECDiffieHellman ExportECDiffieHellman() =>
        DataProtector.ExportAsymmetricAlgorithm(
            ProtectedPkcs8PrivateKey,
            ECDiffieHellman.Create);
}

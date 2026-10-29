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
    int curveSizeBits,
    ReadOnlyMemory<byte> protectedPkcs8PrivateKey,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : EccSecretKey(metadata, curveSizeBits, certificateRawData, issuerCertificatesRawData)
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

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// Describes how <see cref="TestCertificates.CreateClientCertificate"/> shapes a client certificate.
/// </summary>
internal sealed class ClientCertificateOptions
{
    public IReadOnlyList<string>? ExtendedKeyUsages { get; init; } =
    [TestCertificates.ClientAuthenticationOid];

    public IReadOnlyList<string> CrlDistributionPoints { get; init; } = [];

    public IReadOnlyList<string> OcspUris { get; init; } = [];

    public IReadOnlyList<X509Extension> ExtraExtensions { get; init; } = [];

    public DateTimeOffset? NotBefore { get; init; }

    public DateTimeOffset? NotAfter { get; init; }

    public byte[]? SerialNumber { get; init; }
}

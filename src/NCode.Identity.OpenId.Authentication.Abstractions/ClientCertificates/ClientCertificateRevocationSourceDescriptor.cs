// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Describes a source of revocation information: the name that configuration selects it by and its position in the
/// default order. A package contributes a descriptor through
/// <c>ICollectionDataSource&lt;ClientCertificateRevocationSourceDescriptor&gt;</c> beside each
/// <see cref="IClientCertificateRevocationSource"/> it registers, so the configured sources are validated when the host
/// starts without constructing any source.
/// </summary>
[PublicAPI]
public sealed class ClientCertificateRevocationSourceDescriptor
{
    /// <summary>
    /// Gets the name of the source, which is the <see cref="IClientCertificateRevocationSource.Name"/> of the source it
    /// describes and the value that configuration names it by. Names are compared ordinally.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the position of the source in the default order, which applies when configuration selects no sources; the
    /// lowest is asked first, and sources of the same position are asked in the ordinal order of their names. The
    /// built-in <c>ocsp</c> source is at 100 and <c>crl</c> at 200.
    /// </summary>
    public int Order { get; init; }
}

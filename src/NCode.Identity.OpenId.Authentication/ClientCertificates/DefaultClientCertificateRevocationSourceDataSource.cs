// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the <see cref="ClientCertificateRevocationSourceDescriptor"/> of each built-in source of revocation
/// information: <c>ocsp</c>, then <c>crl</c> in the default order. Each descriptor has the name of the
/// <see cref="IClientCertificateRevocationSource"/> it describes.
/// </summary>
// See ADR-1400.
internal sealed class DefaultClientCertificateRevocationSourceDataSource(
    INullChangeToken nullChangeToken
) : ICollectionDataSource<ClientCertificateRevocationSourceDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<ClientCertificateRevocationSourceDescriptor> Collection
    {
        get
        {
            // OCSP first: a response answers for one certificate and is usually fresher than a list (ADR-1259).
            yield return new ClientCertificateRevocationSourceDescriptor
            {
                Name = ClientCertificateRevocationSourceNames.Ocsp,
                Order = 100,
            };

            yield return new ClientCertificateRevocationSourceDescriptor
            {
                Name = ClientCertificateRevocationSourceNames.Crl,
                Order = 200,
            };
        }
    }
}

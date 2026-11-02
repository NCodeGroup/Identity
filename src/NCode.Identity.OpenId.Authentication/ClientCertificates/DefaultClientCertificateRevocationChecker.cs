// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateRevocationChecker"/>. It asks each registered
/// <see cref="IClientCertificateRevocationSource"/> in registration order and returns the first answer that is not
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>; the status is
/// <see cref="ClientCertificateRevocationStatus.Unknown"/> when every source is inconclusive or none is registered.
/// </summary>
// See ADR-1259.
internal sealed class DefaultClientCertificateRevocationChecker(
    IEnumerable<IClientCertificateRevocationSource> sources
) : IClientCertificateRevocationChecker
{
    private IReadOnlyList<IClientCertificateRevocationSource> Sources { get; } = [.. sources];

    /// <inheritdoc />
    public async ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var source in Sources)
        {
            var status = await source.CheckAsync(context, cancellationToken);
            if (status != ClientCertificateRevocationStatus.Unknown)
                return status;
        }

        return ClientCertificateRevocationStatus.Unknown;
    }
}

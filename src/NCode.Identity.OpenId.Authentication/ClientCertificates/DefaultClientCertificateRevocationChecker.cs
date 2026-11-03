// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateRevocationChecker"/>. It asks the sources that
/// <see cref="ClientCertificateRevocationOptions.Sources"/> names, in that order, or every registered
/// <see cref="IClientCertificateRevocationSource"/> in the default order of the source catalog when the option names
/// none, and returns the first answer that is not <see cref="ClientCertificateRevocationStatus.Unknown"/>; the status is
/// <see cref="ClientCertificateRevocationStatus.Unknown"/> when every selected source is inconclusive. The constructor
/// throws an <see cref="InvalidOperationException"/> unless every registered source has a
/// <see cref="ClientCertificateRevocationSourceDescriptor"/>, no two sources share a name, and every described source is
/// registered.
/// </summary>
// See ADR-1259 and ADR-1400.
internal sealed class DefaultClientCertificateRevocationChecker(
    IEnumerable<IClientCertificateRevocationSource> sources,
    IClientCertificateRevocationSourceCatalog catalog,
    IOptions<ClientCertificateRevocationOptions> options
) : IClientCertificateRevocationChecker
{
    private ImmutableArray<IClientCertificateRevocationSource> Sources { get; } =
        SelectSources([.. sources], catalog, options.Value);

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

    // The options validator has already checked the configured names against the catalog when the host started, so a
    // name the catalog lacks here is a source a replaced catalog does not describe.
    private static ImmutableArray<IClientCertificateRevocationSource> SelectSources(
        ImmutableArray<IClientCertificateRevocationSource> sources,
        IClientCertificateRevocationSourceCatalog catalog,
        ClientCertificateRevocationOptions options
    )
    {
        var sourcesByName = new Dictionary<string, IClientCertificateRevocationSource>(
            StringComparer.Ordinal
        );

        foreach (var source in sources)
        {
            var name = source.Name;

            if (!catalog.TryGet(name, out _))
                throw new InvalidOperationException(
                    $"The client-certificate revocation source '{source.GetType().Name}' is named '{name}', which no revocation source descriptor names."
                );

            if (!sourcesByName.TryAdd(name, source))
                throw new InvalidOperationException(
                    $"More than one client-certificate revocation source is named '{name}'."
                );
        }

        foreach (var name in catalog.SourceNames)
        {
            if (!sourcesByName.ContainsKey(name))
                throw new InvalidOperationException(
                    $"No client-certificate revocation source is registered for the described source '{name}'."
                );
        }

        IEnumerable<string> selected =
            options.Sources.Count > 0 ? options.Sources : catalog.SourceNames;

        var builder = ImmutableArray.CreateBuilder<IClientCertificateRevocationSource>();
        foreach (var name in selected)
        {
            if (!sourcesByName.TryGetValue(name, out var source))
                throw new InvalidOperationException(
                    $"The configured client-certificate revocation source '{name}' is not registered."
                );

            builder.Add(source);
        }

        return builder.ToImmutable();
    }
}

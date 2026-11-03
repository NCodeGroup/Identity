// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateRevocationSourceCatalog"/> over the registered
/// <see cref="ClientCertificateRevocationSourceDescriptor"/> data sources.
/// </summary>
/// <remarks>
/// <para>
/// The descriptors are static, so the constructor reads each data source once and does not watch its change token.
/// </para>
/// <para>
/// The constructor throws an <see cref="InvalidOperationException"/> when the registered descriptors are inconsistent:
/// a descriptor has an empty name, a name that contains whitespace, or the same name as another descriptor.
/// </para>
/// </remarks>
// See ADR-1400.
internal sealed class DefaultClientCertificateRevocationSourceCatalog
    : IClientCertificateRevocationSourceCatalog
{
    private FrozenDictionary<
        string,
        ClientCertificateRevocationSourceDescriptor
    > DescriptorsByName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultClientCertificateRevocationSourceCatalog"/> class.
    /// </summary>
    /// <param name="dataSources">The data sources that contribute the revocation-source descriptors.</param>
    public DefaultClientCertificateRevocationSourceCatalog(
        IEnumerable<ICollectionDataSource<ClientCertificateRevocationSourceDescriptor>> dataSources
    )
    {
        var descriptorsByName = new Dictionary<string, ClientCertificateRevocationSourceDescriptor>(
            StringComparer.Ordinal
        );

        foreach (var dataSource in dataSources)
        {
            foreach (var descriptor in dataSource.Collection)
            {
                var name = descriptor.Name;

                if (string.IsNullOrEmpty(name) || name.Any(char.IsWhiteSpace))
                    throw new InvalidOperationException(
                        "A client-certificate revocation source descriptor must have a name that is not empty and contains no whitespace."
                    );

                if (!descriptorsByName.TryAdd(name, descriptor))
                    throw new InvalidOperationException(
                        $"More than one client-certificate revocation source is named '{name}'."
                    );
            }
        }

        DescriptorsByName = descriptorsByName.ToFrozenDictionary(StringComparer.Ordinal);
        SourceNames = descriptorsByName
            .Values.OrderBy(static descriptor => descriptor.Order)
            .ThenBy(static descriptor => descriptor.Name, StringComparer.Ordinal)
            .Select(static descriptor => descriptor.Name)
            .ToImmutableArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SourceNames { get; }

    /// <inheritdoc />
    public bool TryGet(
        string sourceName,
        [NotNullWhen(true)] out ClientCertificateRevocationSourceDescriptor? descriptor
    ) => DescriptorsByName.TryGetValue(sourceName, out descriptor);
}

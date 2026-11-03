// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientAuthenticationMethodCatalog"/> over the registered
/// <see cref="ClientAuthenticationMethodDescriptor"/> data sources. Methods are keyed by their
/// <see cref="ClientAuthenticationMethodDescriptor.Name"/>.
/// </summary>
/// <remarks>
/// <para>
/// The descriptors are static, so the constructor reads each data source once and does not watch its change token.
/// </para>
/// <para>
/// The constructor throws an <see cref="InvalidOperationException"/> when the registered descriptors are inconsistent:
/// a descriptor has an empty name, a name that contains whitespace, or the same name as another descriptor, or it uses
/// the client secret as a key without requiring one.
/// </para>
/// </remarks>
// See ADR-1207.
internal sealed class DefaultClientAuthenticationMethodCatalog : IClientAuthenticationMethodCatalog
{
    private FrozenDictionary<
        string,
        ClientAuthenticationMethodDescriptor
    > DescriptorsByName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultClientAuthenticationMethodCatalog"/> class.
    /// </summary>
    /// <param name="dataSources">The data sources that contribute the client-authentication method descriptors.</param>
    public DefaultClientAuthenticationMethodCatalog(
        IEnumerable<ICollectionDataSource<ClientAuthenticationMethodDescriptor>> dataSources
    )
    {
        var descriptorsByName = new Dictionary<string, ClientAuthenticationMethodDescriptor>(
            StringComparer.Ordinal
        );
        var methodNames = ImmutableArray.CreateBuilder<string>();

        foreach (var dataSource in dataSources)
        {
            foreach (var descriptor in dataSource.Collection)
            {
                var name = descriptor.Name;

                if (string.IsNullOrEmpty(name) || name.Any(char.IsWhiteSpace))
                    throw new InvalidOperationException(
                        "A client-authentication method descriptor must have a name that is not empty and contains no whitespace."
                    );

                // A method that keys from the client_secret needs one issued; see ADR-1391.
                if (descriptor.UsesClientSecretAsKey && !descriptor.RequiresClientSecret)
                    throw new InvalidOperationException(
                        $"The client-authentication method '{name}' uses the client secret as a key but does not require one."
                    );

                if (!descriptorsByName.TryAdd(name, descriptor))
                    throw new InvalidOperationException(
                        $"More than one client-authentication method is named '{name}'."
                    );

                methodNames.Add(name);
            }
        }

        DescriptorsByName = descriptorsByName.ToFrozenDictionary(StringComparer.Ordinal);
        MethodNames = methodNames.ToImmutable();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> MethodNames { get; }

    /// <inheritdoc />
    public bool TryGet(
        string methodName,
        [NotNullWhen(true)] out ClientAuthenticationMethodDescriptor? descriptor
    ) => DescriptorsByName.TryGetValue(methodName, out descriptor);
}

#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
/// a descriptor has an empty name, a name that contains whitespace, or the same name as another descriptor.
/// </para>
/// </remarks>
// See ADR-0207.
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

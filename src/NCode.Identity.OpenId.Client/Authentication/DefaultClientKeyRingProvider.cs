// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides a default implementation of <see cref="IClientKeyRingProvider"/> that resolves a key ring as the keyed
/// <see cref="ISecretKeyCollectionProvider"/> registered under its name, and answers <c>HasKeyRing</c> without
/// resolving the key ring.
/// </summary>
internal class DefaultClientKeyRingProvider(
    IServiceProvider serviceProvider,
    IServiceProviderIsKeyedService keyedServices
) : IClientKeyRingProvider
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    private IServiceProviderIsKeyedService KeyedServices { get; } = keyedServices;

    /// <inheritdoc />
    public ISecretKeyCollection GetKeyRing(string keyRing)
    {
        var provider = ServiceProvider.GetKeyedService<ISecretKeyCollectionProvider>(keyRing);
        return provider?.Collection
            ?? throw new InvalidOperationException($"No key ring named '{keyRing}' is registered.");
    }

    /// <inheritdoc />
    public bool HasKeyRing(string keyRing)
    {
        ArgumentNullException.ThrowIfNull(keyRing);
        return KeyedServices.IsKeyedService(typeof(ISecretKeyCollectionProvider), keyRing);
    }
}

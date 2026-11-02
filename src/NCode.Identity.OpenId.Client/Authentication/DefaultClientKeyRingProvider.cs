// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides a default implementation of <see cref="IClientKeyRingProvider"/> that resolves a key ring as the keyed
/// <see cref="ISecretKeyCollectionProvider"/> registered under its name.
/// </summary>
internal class DefaultClientKeyRingProvider(IServiceProvider serviceProvider)
    : IClientKeyRingProvider
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ISecretKeyCollection GetKeyRing(string keyRing)
    {
        var provider = ServiceProvider.GetKeyedService<ISecretKeyCollectionProvider>(keyRing);
        return provider?.Collection
            ?? throw new InvalidOperationException($"No key ring named '{keyRing}' is registered.");
    }
}

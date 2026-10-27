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

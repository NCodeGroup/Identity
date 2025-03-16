#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdClientFactory"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultOpenIdClientFactory(
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IOpenIdClientFactory
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } = secretKeyCollectionFactory;

    /// <inheritdoc />
    public ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        IReadOnlySettingCollection settings,
        IReadOnlyCollection<SecretKey> secrets,
        CancellationToken cancellationToken
    )
    {
        var secretKeys = SecretKeyCollectionFactory.Create(secrets);
        var propertyBag = openIdContext.PropertyBag.Clone();

        var redirectUris = GetRedirectUris(settings);

        var publicClient = CreatePublicClient(
            clientId,
            settings,
            secretKeys,
            redirectUris,
            propertyBag
        );

        return ValueTask.FromResult(publicClient);
    }

    /// <summary>
    /// Gets the collection of redirect URIs from the client settings.
    /// </summary>
    protected internal virtual IReadOnlyCollection<string> GetRedirectUris(IReadOnlySettingCollection settings)
    {
        if (!settings.TryGetValue(SettingKeys.RedirectUris, out var redirectUris))
        {
            redirectUris = [];
        }

        return redirectUris;
    }

    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdClient"/> that represents a public client.
    /// </summary>
    protected internal virtual OpenIdClient CreatePublicClient(
        string clientId,
        IReadOnlySettingCollection settings,
        ISecretKeyCollection secretKeys,
        IReadOnlyCollection<string> redirectUris,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdClient(
            clientId,
            settings,
            secretKeys,
            redirectUris,
            propertyBag
        );

    /// <inheritdoc />
    public ValueTask<OpenIdConfidentialClient> CreateConfidentialClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken
    )
    {
        OpenIdConfidentialClient confidentialClient = new DefaultOpenIdConfidentialClient(
            publicClient,
            method,
            secretKey,
            confirmation
        );

        return ValueTask.FromResult(confidentialClient);
    }
}

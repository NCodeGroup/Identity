// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdClientFactory"/> abstraction.
/// </summary>
internal class DefaultOpenIdClientFactory(ISecretKeyCollectionFactory secretKeyCollectionFactory)
    : IOpenIdClientFactory
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } =
        secretKeyCollectionFactory;

    /// <inheritdoc />
    public ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        OpenIdClientDefinition definition,
        CancellationToken cancellationToken
    )
    {
        var secretKeys = SecretKeyCollectionFactory.Create(definition.Secrets);
        var propertyBag = openIdContext.PropertyBag.Clone();

        var redirectUris = GetRedirectUris(definition.Settings);

        var publicClient = CreatePublicClient(definition, secretKeys, redirectUris, propertyBag);

        return ValueTask.FromResult(publicClient);
    }

    /// <summary>
    /// Gets the collection of redirect URIs from the client settings.
    /// </summary>
    protected internal virtual IReadOnlyCollection<string> GetRedirectUris(
        IReadOnlySettingCollection settings
    )
    {
        if (!settings.TryGetValue(OpenIdSettingKeys.RedirectUris, out var redirectUris))
        {
            redirectUris = [];
        }

        return redirectUris;
    }

    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdClient"/> that represents a public client.
    /// </summary>
    protected internal virtual OpenIdClient CreatePublicClient(
        OpenIdClientDefinition definition,
        ISecretKeyCollection secretKeys,
        IReadOnlyCollection<string> redirectUris,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdClient(
            definition.ClientId,
            definition.Settings,
            secretKeys,
            redirectUris,
            definition.ResourceServerId,
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

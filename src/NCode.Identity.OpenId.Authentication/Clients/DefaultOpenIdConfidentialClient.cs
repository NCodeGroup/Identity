// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdConfidentialClient"/> abstraction.
/// </summary>
internal class DefaultOpenIdConfidentialClient(
    OpenIdClient publicClient,
    string authenticationMethod,
    SecretKey secretKey,
    JsonElement? confirmation
) : OpenIdConfidentialClient
{
    private OpenIdClient PublicClient { get; } = publicClient;

    /// <inheritdoc />
    public override string ClientId => PublicClient.ClientId;

    /// <inheritdoc />
    public override IReadOnlySettingCollection Settings => PublicClient.Settings;

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys => PublicClient.SecretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<string> RedirectUris => PublicClient.RedirectUris;

    /// <inheritdoc />
    public override string? ResourceServerId => PublicClient.ResourceServerId;

    /// <inheritdoc />
    public override bool IsConfidential => true;

    /// <inheritdoc />
    public override OpenIdConfidentialClient ConfidentialClient => this;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag => PublicClient.PropertyBag;

    /// <inheritdoc />
    public override string AuthenticationMethod { get; } = authenticationMethod;

    /// <inheritdoc />
    public override SecretKey SecretKey { get; } = secretKey;

    /// <inheritdoc />
    public override JsonElement? Confirmation { get; } = confirmation;
}

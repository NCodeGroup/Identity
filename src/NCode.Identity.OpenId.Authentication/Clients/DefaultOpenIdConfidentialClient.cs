// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Verifiers;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdConfidentialClient"/> abstraction.
/// </summary>
internal class DefaultOpenIdConfidentialClient(
    OpenIdClient publicClient,
    string authenticationMethod,
    string? credentialId,
    JsonElement? confirmation
) : OpenIdConfidentialClient
{
    private OpenIdClient PublicClient { get; } = publicClient;

    /// <inheritdoc />
    public override string ClientId => PublicClient.ClientId;

    /// <inheritdoc />
    public override string PrincipalId => PublicClient.PrincipalId;

    /// <inheritdoc />
    public override IReadOnlySettingCollection Settings => PublicClient.Settings;

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys => PublicClient.SecretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<SecretVerifier> SecretVerifiers =>
        PublicClient.SecretVerifiers;

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
    public override string? CredentialId { get; } = credentialId;

    /// <inheritdoc />
    public override JsonElement? Confirmation { get; } = confirmation;
}

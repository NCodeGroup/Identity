// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Verifiers;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdClient"/> abstraction.
/// </summary>
internal class DefaultOpenIdClient(
    string clientId,
    string principalId,
    IReadOnlySettingCollection settings,
    ISecretKeyCollection secretKeys,
    IReadOnlyCollection<SecretVerifier> secretVerifiers,
    IReadOnlyCollection<string> redirectUris,
    string? resourceServerId,
    IPropertyBag propertyBag
) : OpenIdClient
{
    /// <inheritdoc />
    public override string ClientId { get; } = clientId;

    /// <inheritdoc />
    public override string PrincipalId { get; } = principalId;

    /// <inheritdoc />
    public override IReadOnlySettingCollection Settings { get; } = settings;

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys { get; } = secretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<SecretVerifier> SecretVerifiers { get; } = secretVerifiers;

    /// <inheritdoc />
    public override IReadOnlyCollection<string> RedirectUris { get; } = redirectUris;

    /// <inheritdoc />
    public override string? ResourceServerId { get; } = resourceServerId;

    /// <inheritdoc />
    public override bool IsConfidential => false;

    /// <inheritdoc />
    public override OpenIdConfidentialClient? ConfidentialClient => null;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Disposables;
using NCode.Identity.Models;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdTenant"/> abstraction.
/// </summary>
internal class DefaultOpenIdTenant(
    string tenantId,
    string displayName,
    string issuer,
    string? mtlsEndpointAliasAuthority,
    UriDescriptor baseAddress,
    AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> settingsReference,
    AsyncSharedReferenceLease<ISecretKeyCollectionProvider> secretsReference,
    IPropertyBag propertyBag
) : OpenIdTenant
{
    private AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> SettingsReference { get; set; } =
        settingsReference.AddReference();
    private AsyncSharedReferenceLease<ISecretKeyCollectionProvider> SecretsReference { get; set; } =
        secretsReference.AddReference();

    /// <inheritdoc />
    public override string TenantId { get; } = tenantId;

    /// <inheritdoc />
    public override string DisplayName { get; } = displayName;

    /// <inheritdoc />
    public override string Issuer { get; } = issuer;

    /// <inheritdoc />
    public override string? MtlsEndpointAliasAuthority { get; } = mtlsEndpointAliasAuthority;

    /// <inheritdoc />
    public override UriDescriptor BaseAddress { get; } = baseAddress;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider => SettingsReference.Value;

    /// <inheritdoc />
    public override ISecretKeyCollectionProvider SecretsProvider => SecretsReference.Value;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await SettingsReference.DisposeAsync();
        SettingsReference = default;

        await SecretsReference.DisposeAsync();
        SecretsReference = default;
    }
}

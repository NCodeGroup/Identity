// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Models;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the configuration for an <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// </summary>
[PublicAPI]
public abstract class OpenIdTenant : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for the tenant.
    /// </summary>
    public abstract string TenantId { get; }

    /// <summary>
    /// Gets the identifier of the team the tenant belongs to. A tenant never moves between teams.
    /// </summary>
    public abstract string TeamId { get; }

    /// <summary>
    /// Gets the display name for the tenant.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the issuer identifier for the tenant.
    /// </summary>
    public abstract string Issuer { get; }

    /// <summary>
    /// Gets the mutual-TLS endpoint-alias authority for the tenant (RFC 8705 §5) — the <c>https</c> origin the discovery
    /// document mirrors the tenant's endpoints under in <c>mtls_endpoint_aliases</c> — or <c>null</c> when the tenant
    /// publishes no aliases.
    /// </summary>
    public abstract string? MtlsEndpointAliasAuthority { get; }

    /// <summary>
    /// Gets the base address for the tenant.
    /// Includes the server base address and tenant path.
    /// </summary>
    public abstract UriDescriptor BaseAddress { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> that contains tenant settings merged with platform settings.
    /// This collection provider will periodically refresh the items from the backing store and provide change notifications.
    /// </summary>
    public abstract IReadOnlySettingCollectionProvider SettingsProvider { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollectionProvider"/> which contains secrets only known to the tenant.
    /// This collection provider will periodically refresh the items from the backing store and provide change notifications.
    /// </summary>
    public abstract ISecretKeyCollectionProvider SecretsProvider { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected abstract ValueTask DisposeAsyncCore();
}

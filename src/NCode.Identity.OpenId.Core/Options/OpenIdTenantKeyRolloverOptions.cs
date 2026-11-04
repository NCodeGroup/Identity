// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options of the rollover of the keys every tenant is provisioned with: how often the background pass
/// runs, and which settings name the lifetimes of the tokens a tenant protects with its keys. The lifetimes of the
/// keys themselves are tenant settings (<c>tenant_key_lifetime</c>, <c>tenant_key_activation_delay</c> and
/// <c>tenant_key_retirement_grace</c>), and the pass runs only while key provisioning is enabled.
/// </summary>
// See ADR-1404.
[PublicAPI]
public sealed class OpenIdTenantKeyRolloverOptions
{
    /// <summary>
    /// Gets or sets how often one replica walks every tenant to generate the successor of a key that nears its
    /// retirement, activate a published key when no key signs, retire a key the tenant's settings no longer use, and
    /// remove an expired key the server generated. A successor is generated when its predecessor retires within the
    /// tenant's activation delay plus this interval, so it is published for at least the delay. The default value is
    /// one hour.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1.0);

    /// <summary>
    /// Gets or sets the names of the settings that hold the lifetimes of the tokens a tenant protects with its keys:
    /// a retired key stays published, and verifies or decrypts, for at least the longest of them plus the tenant's
    /// clock skew, whatever <c>tenant_key_retirement_grace</c> says, so no token outlives the key that verifies it.
    /// </summary>
    /// <remarks>
    /// A package that signs or encrypts another kind of token with the tenant's keys adds its lifetime setting here.
    /// Configuration binding adds to the default names rather than replacing them.
    /// </remarks>
    public List<string> TokenLifetimeSettings { get; set; } =
    [
        OpenIdSettingNames.IdTokenLifetime,
        OpenIdSettingNames.AccessTokenLifetime,
        OpenIdSettingNames.AuthorizationResponseLifetime,
        OpenIdSettingNames.RequestObjectMaxLifetime,
    ];
}

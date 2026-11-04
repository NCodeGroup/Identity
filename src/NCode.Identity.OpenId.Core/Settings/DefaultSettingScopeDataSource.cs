// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the setting scopes of the OpenID server: the server is the root of the cascade, the tenant inherits the
/// server's settings, and each client and each resource server inherits its tenant's. A package that stores settings for
/// an entity of its own contributes its scope through another data source, beneath the scope it inherits from.
/// </summary>
// See ADR-1149.
internal class DefaultSettingScopeDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingScopeDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingScopeDescriptor> Collection
    {
        get
        {
            yield return new SettingScopeDescriptor { Name = OpenIdSettingScopes.Platform };

            yield return new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.Tenant,
                ParentName = OpenIdSettingScopes.Platform,
            };

            yield return new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.Client,
                ParentName = OpenIdSettingScopes.Tenant,
            };

            yield return new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.ResourceServer,
                ParentName = OpenIdSettingScopes.Tenant,
            };
        }
    }
}

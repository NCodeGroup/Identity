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

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the setting scopes of the OpenID server: the server is the root of the cascade, the tenant inherits the
/// server's settings, and each client and each resource server inherits its tenant's. A package that stores settings for
/// an entity of its own contributes its scope through another data source, beneath the scope it inherits from.
/// </summary>
// See ADR-0149.
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
            yield return new SettingScopeDescriptor { Name = OpenIdSettingScopes.Server };

            yield return new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.Tenant,
                ParentName = OpenIdSettingScopes.Server,
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

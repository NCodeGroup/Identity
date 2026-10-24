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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the settings whose default value or domain
/// is the registered client-authentication methods, named by their descriptors (an authentication-slice concern),
/// composed with the base descriptor catalog from the core slice. Each such setting accepts only the registered
/// authentication methods, so a misspelled or unsupported method is rejected rather than stored and never matched.
/// </summary>
// See ADR-0140 and ADR-0207.
internal class DefaultClientSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog
) : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IClientAuthenticationMethodCatalog ClientAuthenticationMethodCatalog { get; } =
        clientAuthenticationMethodCatalog;

    private IReadOnlyList<string> AuthMethods => ClientAuthenticationMethodCatalog.MethodNames;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // token_endpoint_auth_methods_supported: the client-authentication methods the server, tenant, or security
            // profile permits (default derived from the registered method descriptors). A ceiling over the registered
            // method, never a choice between methods. Ceiling (Intersect): a child narrows the parent's set; unset =
            // unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethodsSupported,
                AppliesTo = [OpenIdSettingScopes.Tenant, OpenIdSettingScopes.Client],
                Rule = SettingRules.Each(AuthMethod()),
                Default = AuthMethods,

                IsDiscoverable = true,
                OnMerge = SettingMerge.Intersect,
            };

            // token_endpoint_auth_method: the one client-authentication method a client is registered to use (RFC 7591
            // §2); it decides the client type (none = public) and is the only method that authenticates the client.
            // A client that registers none gets the RFC 7591 default, client_secret_basic, unless its server or tenant
            // configures another. Not advertised. Override (Replace): a child fully replaces the parent value
            // (ADR-0145).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethod,
                AppliesTo = [OpenIdSettingScopes.Client],
                Rule = AuthMethod(),
                Default = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };
        }
    }

    private SettingRule<string> AuthMethod() => SettingRules.OneOf(AuthMethods);
}

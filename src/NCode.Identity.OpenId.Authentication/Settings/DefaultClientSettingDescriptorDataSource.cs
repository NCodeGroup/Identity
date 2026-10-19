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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the settings whose default value or domain
/// depends on the registered client-authentication handlers (an authentication-slice concern), composed with the base
/// descriptor catalog from the core slice. Each such setting accepts only the authentication methods of the registered
/// handlers, so a misspelled or unsupported method is rejected rather than stored and never matched.
/// </summary>
// See ADR-0140.
internal class DefaultClientSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IServiceProvider serviceProvider
) : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    private List<string>? AuthMethodsOrNull { get; set; }
    private List<string> AuthMethods => AuthMethodsOrNull ??= GetAuthMethods();

    private List<string> GetAuthMethods()
    {
        return ServiceProvider
            .GetServices<IClientAuthenticationHandler>()
            .Select(handler => handler.AuthenticationMethod)
            .ToList();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // token_endpoint_auth_methods_supported: client authentication methods the token endpoint accepts (default derived from the registered handlers).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethodsSupported,
                Rule = SettingRules.Each(AuthMethod()),
                Default = AuthMethods,

                IsDiscoverable = true,
                OnMerge = SettingMerge.Intersect,
            };

            // token_endpoint_auth_method: the single client-authentication method a client is registered to use at the
            // token endpoint (per-client; selects which mutual-TLS handler, if any, owns the request); empty (the
            // default) means the client registered none. Not advertised. Override (Replace): a child fully replaces
            // the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethod,
                Rule = SettingRules.EmptyOr(AuthMethod()),
                Default = string.Empty,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };
        }
    }

    private SettingRule<string> AuthMethod() => SettingRules.OneOf(AuthMethods);
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
// See ADR-1140 and ADR-1207.
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
            // unrestricted (ADR-1010).
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
            // configures another, and every entry point that stores a client writes that method into its document, so
            // a later change to the server's or tenant's value moves no stored client's method (ADR-1391). Not
            // advertised. Override (Replace): a child fully replaces the parent value (ADR-1145).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethod,
                AppliesTo = [OpenIdSettingScopes.Client],
                Rule = AuthMethod(),
                Default = OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };
        }
    }

    private SettingRule<string> AuthMethod() => SettingRules.OneOf(AuthMethods);
}

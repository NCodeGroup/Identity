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

using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the client-authentication registration of an <see cref="OpenIdClient"/>: the one method it is registered to
/// use (RFC 7591 §2), the client type that method decides (RFC 6749 §2.1), and whether the server, tenant, and security
/// profile permit it. Every endpoint that authenticates a client accepts only its registered method, so a client that
/// authenticated is always of the type it is registered as; the authorization endpoint, which does not authenticate the
/// client, reads the registered type directly.
/// </summary>
// See ADR-0145.
internal static class OpenIdClientRegistrationExtensions
{
    extension(OpenIdClient client)
    {
        /// <summary>
        /// Gets the client-authentication method the client is registered to use: its effective
        /// <c>token_endpoint_auth_method</c> setting, which is <c>client_secret_basic</c> when neither the client nor
        /// its server or tenant registers one.
        /// </summary>
        public string RegisteredAuthenticationMethod
        {
            get
            {
                ArgumentNullException.ThrowIfNull(client);

                return client.Settings.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the client is registered as a confidential client: its registered
        /// client-authentication method is not <c>none</c>. This is the type the client is registered as, which the
        /// authorization endpoint reads because it does not authenticate the client; an endpoint that authenticates
        /// the client reads <see cref="OpenIdClient.IsConfidential"/>, which agrees with it.
        /// </summary>
        public bool IsRegisteredAsConfidential =>
            !string.Equals(
                client.RegisteredAuthenticationMethod,
                OpenIdConstants.ClientAuthenticationMethods.None,
                StringComparison.Ordinal
            );

        /// <summary>
        /// Gets a value indicating whether the client's effective <c>token_endpoint_auth_methods_supported</c> — the
        /// ceiling its server, tenant, and security profile impose — permits its registered client-authentication
        /// method. An unset ceiling permits every method; a client whose registered method the ceiling excludes cannot
        /// authenticate at all.
        /// </summary>
        public bool IsRegisteredAuthenticationMethodPermitted
        {
            get
            {
                var registeredMethod = client.RegisteredAuthenticationMethod;

                return !client.Settings.TryGetValue(
                        OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                        out var permittedMethods
                    ) || permittedMethods.Contains(registeredMethod, StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Determines whether the client may authenticate with the specified client-authentication
        /// <paramref name="method"/>: it is the method the client is registered to use, and the client's ceiling permits
        /// it.
        /// </summary>
        /// <param name="method">The client-authentication method the client authenticated, or tries to authenticate,
        /// with.</param>
        /// <returns><see langword="true"/> when the client may authenticate with <paramref name="method"/>.</returns>
        public bool MayAuthenticateWith(string method) =>
            string.Equals(client.RegisteredAuthenticationMethod, method, StringComparison.Ordinal)
            && client.IsRegisteredAuthenticationMethodPermitted;
    }
}

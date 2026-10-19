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
/// Provides the client type an <see cref="OpenIdClient"/> is registered as (RFC 6749 §2.1), which the authorization
/// endpoint relies on because no client authentication takes place there.
/// </summary>
internal static class OpenIdClientRegistrationExtensions
{
    extension(OpenIdClient client)
    {
        /// <summary>
        /// Gets a value indicating whether the client is registered as a confidential client: its registered
        /// <c>token_endpoint_auth_method</c> (RFC 7591 §2) names a client-authentication method other than <c>none</c>.
        /// A client that registered no method, or <c>none</c>, is a public client. This is the type the client is
        /// registered as, not whether the current request authenticated it (<see cref="OpenIdClient.IsConfidential"/>).
        /// </summary>
        public bool IsRegisteredAsConfidential
        {
            get
            {
                ArgumentNullException.ThrowIfNull(client);

                var authMethod = client.Settings.GetValue(
                    OpenIdSettingKeys.TokenEndpointAuthMethod
                );
                return !string.IsNullOrEmpty(authMethod)
                    && !string.Equals(
                        authMethod,
                        OpenIdConstants.ClientAuthenticationMethods.None,
                        StringComparison.Ordinal
                    );
            }
        }
    }
}

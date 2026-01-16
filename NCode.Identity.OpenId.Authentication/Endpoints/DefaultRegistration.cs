#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery;
using NCode.Identity.OpenId.Authentication.Endpoints.Token;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Authentication endpoints.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Authentication endpoints.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddEndpoints()
        {
            var newBuilder = builder.NewBuilder<OpenIdAuthenticationEndpoints>();

            newBuilder.AddAuthorizationEndpoint();
            newBuilder.AddContinueEndpoint();
            newBuilder.AddDiscoveryEndpoint();
            newBuilder.AddTokenEndpoint();

            return builder;
        }
    }
}

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
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>Token Revocation</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IServiceBuilder<OpenIdAuthenticationEndpoints> builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>Token Revocation</c> endpoint (RFC 7009).
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationEndpoints> AddRevocationEndpoint()
        {
            builder.AddMessageFactory<TokenRevocationRequest>();

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<RevokeTokenCommand>,
                    DefaultRevokeTokenHandler
                >()
            );

            return builder;
        }
    }
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>Token Revocation</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>Token Revocation</c> endpoint (RFC 7009).
        /// </summary>
        public void AddRevocationEndpoint()
        {
            builder.AddEndpoint<DefaultRevocationEndpointProvider>();

            builder.AddMessageFactory<TokenRevocationRequest>();

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<RevokeTokenCommand>,
                    DefaultRevokeTokenHandler
                >()
            );
        }
    }
}

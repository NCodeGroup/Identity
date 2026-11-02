// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>Token Introspection</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>Token Introspection</c> endpoint (RFC 7662).
        /// </summary>
        public void AddIntrospectionEndpoint()
        {
            builder.AddEndpoint<DefaultIntrospectionEndpointProvider>();

            builder.AddMessageFactory<TokenIntrospectionRequest>();

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<IntrospectTokenCommand>,
                    DefaultIntrospectTokenHandler
                >()
            );
        }
    }
}

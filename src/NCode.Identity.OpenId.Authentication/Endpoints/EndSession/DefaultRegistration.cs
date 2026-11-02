// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId RP-Initiated Logout
/// <c>end_session_endpoint</c>.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId RP-Initiated Logout <c>end_session_endpoint</c>
        /// (RP-Initiated Logout 1.0).
        /// </summary>
        public void AddEndSessionEndpoint()
        {
            builder.AddEndpoint<DefaultEndSessionEndpointProvider>();

            builder.AddMessageFactory<EndSessionRequest>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<LoadEndSessionRequestCommand, IEndSessionRequest>,
                DefaultLoadEndSessionRequestHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<ResolveEndSessionSubjectCommand, EndSessionSubject>,
                DefaultResolveEndSessionSubjectHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<EndSessionCommand, EndSessionDisposition>,
                DefaultEndSessionHandler
            >();
        }
    }
}

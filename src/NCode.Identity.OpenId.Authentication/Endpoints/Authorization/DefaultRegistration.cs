// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.Registration.AspNetCore;
using NCode.Identity.Results;
using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Authorization endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId Authorization endpoint.
        /// </summary>
        public void AddAuthorizationEndpoint()
        {
            builder.AddEndpoint<DefaultAuthorizationEndpointHandler>();

            // Messages
            builder.AddMessageFactory<AuthorizationRequestMessage>();
            builder.AddMessageFactory<AuthorizationRequestObject>();
            builder.AddMessageFactory<AuthorizationTicket>();

            var serviceCollection = builder.ServiceCollection;

            // Request processing
            serviceCollection.TryAddSingleton<
                IAuthorizationRequestProcessor,
                DefaultAuthorizationRequestProcessor
            >();
            serviceCollection.TryAddSingleton<
                IResultExecutor<AuthorizationResult>,
                DefaultAuthorizationResultExecutor
            >();
            serviceCollection.TryAddSingleton<
                IAuthorizationResponseModeProvider,
                DefaultAuthorizationResponseModeProvider
            >();
            serviceCollection.TryAddSingleton<
                IAuthorizationResponseDeliveryResolver,
                DefaultAuthorizationResponseDeliveryResolver
            >();

            // Response modes are an open vocabulary: a package adds one by registering its handler (ADR-1201).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    QueryAuthorizationResponseModeHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    FragmentAuthorizationResponseModeHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    FormPostAuthorizationResponseModeHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    QueryJwtAuthorizationResponseModeHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    FragmentJwtAuthorizationResponseModeHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IAuthorizationResponseModeHandler,
                    FormPostJwtAuthorizationResponseModeHandler
                >()
            );

            // Continue Providers
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IContinueProvider,
                    DefaultAuthorizationContinueProvider
                >()
            );

            // Serialization
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IOpenIdJsonConverterProvider,
                    AuthorizationJsonConverterProvider
                >()
            );

            // Mediator
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>,
                DefaultLoadAuthorizationRequestHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandHandler<ValidateAuthorizationRequestCommand>,
                DefaultValidateAuthorizationRequestHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<AuthenticateCommand, AuthenticateSubjectDisposition>,
                DefaultAuthenticateHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponsePostProcessor<AuthenticateCommand, AuthenticateSubjectDisposition>,
                DefaultAuthenticatePostProcessor
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<AuthorizeCommand, AuthorizeDisposition>,
                DefaultAuthorizeHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponsePostProcessor<AuthorizeCommand, AuthorizeDisposition>,
                DefaultAuthorizePostProcessor
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<ChallengeCommand, ReadOnlyEndpointDisposition>,
                DefaultChallengeHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>,
                DefaultConsentChallengeHandler
            >();
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>,
                DefaultCreateAuthorizationTicketHandler
            >();
        }
    }
}

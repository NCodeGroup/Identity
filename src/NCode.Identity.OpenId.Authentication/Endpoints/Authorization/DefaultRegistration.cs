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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.Results;
using NCode.Mediator;
using NCode.Mediator.Middleware;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Authorization endpoint.
/// </summary>
[PublicAPI]
internal static class DefaultRegistration
{
    extension(IServiceBuilder<OpenIdAuthenticationEndpoints> builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId Authorization endpoint.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationEndpoints> AddAuthorizationEndpoint()
        {
            // Endpoints
            builder.AddOpenIdEndpointProvider<DefaultAuthorizationEndpointHandler>();

            // Messages
            builder.AddMessageFactory<AuthorizationRequestMessage>();
            builder.AddMessageFactory<AuthorizationRequestObject>();
            builder.AddMessageFactory<AuthorizationTicket>();

            var serviceCollection = builder.ServiceCollection;

            // Logic
            serviceCollection.TryAddSingleton<
                IAuthorizationEndpointLogic,
                DefaultAuthorizationEndpointLogic
            >();
            serviceCollection.TryAddSingleton<
                IResultExecutor<AuthorizationResult>,
                DefaultAuthorizationResultExecutor
            >();

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
                ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>,
                DefaultCreateAuthorizationTicketHandler
            >();

            return builder;
        }
    }
}

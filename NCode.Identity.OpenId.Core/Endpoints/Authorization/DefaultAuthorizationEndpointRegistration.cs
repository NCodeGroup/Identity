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
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Endpoints.Authorization.Logic;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Mediator.Middleware;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the authorization endpoint.
/// </summary>
[PublicAPI]
public static class DefaultAuthorizationEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the authorization endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddAuthorizationEndpoint(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<
            IAuthorizationCodeService,
            DefaultAuthorizationCodeService>();

        serviceCollection.AddSingleton<DefaultAuthorizationEndpointHandler>();

        serviceCollection.TryAddSingleton<IContinueProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.TryAddSingleton<IOpenIdEndpointProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>,
            DefaultLoadAuthorizationSourceHandler>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>,
            DefaultLoadAuthorizationRequestHandler>();

        serviceCollection.TryAddSingleton<
            ICommandHandler<ValidateAuthorizationRequestCommand>,
            DefaultValidateAuthorizationRequestHandler>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>,
            DefaultAuthenticateSubjectHandler>();

        serviceCollection.TryAddSingleton<
            ICommandResponsePostProcessor<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>,
            DefaultAuthenticateSubjectPostProcessor>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>,
            DefaultAuthorizeSubjectHandler>();

        serviceCollection.TryAddSingleton<
            ICommandResponsePostProcessor<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>,
            DefaultAuthorizeSubjectPostProcessor>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<ChallengeSubjectCommand, OperationDisposition>,
            DefaultChallengeSubjectHandler>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>,
            DefaultCreateAuthorizationTicketHandler>();

        serviceCollection.TryAddSingleton<
            IResultExecutor<AuthorizationResult>,
            AuthorizationResultExecutor>();

        return serviceCollection;
    }
}

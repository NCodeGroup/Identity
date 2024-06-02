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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Logic;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the authorization endpoint.
/// </summary>
public static class AuthorizationEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the authorization endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddAuthorizationEndpoint(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IAuthorizationCodeService, DefaultAuthorizationCodeService>();
        serviceCollection.AddSingleton<IAuthorizationInteractionService, NullAuthorizationInteractionService>();

        serviceCollection.AddSingleton<DefaultAuthorizationEndpointHandler>();

        serviceCollection.AddSingleton<IContinueProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<IOpenIdEndpointProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandHandler<ValidateAuthorizationRequestCommand>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandResponseHandler<AuthenticateCommand, AuthenticateSubjectResult>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandResponseHandler<AuthorizeCommand, IResult?>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        serviceCollection.AddSingleton<IResultExecutor<AuthorizationResult>, AuthorizationResultExecutor>();

        return serviceCollection;
    }
}

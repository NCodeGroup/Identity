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

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Logic.Authorization;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.ServiceCollectionExtensions;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the authorization endpoint.
/// </summary>
public static class AuthorizationEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the authorization endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddAuthorizationEndpoint(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationTicketService, AuthorizationTicketService>();
        services.AddSingleton<IAuthorizationClaimsService, AuthorizationClaimsService>();
        services.AddSingleton<IAuthorizationLoginService, NullAuthorizationLoginService>();
        services.AddSingleton<IAuthorizationCallbackService, NullAuthorizationCallbackService>();

        services.AddSingleton<DefaultAuthorizationEndpointProvider>();
        services.AddSingleton<DefaultAuthorizationEndpointHandler>();

        services.AddSingleton<IOpenIdEndpointProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointProvider>());

        services.AddSingleton<ICommandResponseHandler<AuthorizationEndpointCommand, IOpenIdResult>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandResponseHandler<LoadAuthorizationRequestCommand, AuthorizationContext>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandHandler<ValidateAuthorizationRequestCommand>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandResponseHandler<AuthenticateCommand, AuthenticateResult>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandResponseHandler<AuthorizeCommand, IOpenIdResult?>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        services.AddSingleton<ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultAuthorizationEndpointHandler>());

        return services;
    }
}

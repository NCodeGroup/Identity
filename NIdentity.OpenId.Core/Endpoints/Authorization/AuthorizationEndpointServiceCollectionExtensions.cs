#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Handlers;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints.Authorization;

public static class AuthorizationEndpointServiceCollectionExtensions
{
    public static IServiceCollection AddAuthorizationEndpoint(this IServiceCollection services)
    {
        services.AddOpenIdEndpoint<AuthorizationEndpointProvider, AuthorizationEndpointHandler, AuthorizationEndpointCommand>();

        services.AddTransient<ICommandResponseHandler<LoadAuthorizationRequestCommand, AuthorizationContext>, LoadAuthorizationRequestHandler>();
        services.AddTransient<ICommandHandler<ValidateAuthorizationRequestCommand>, ValidateAuthorizationRequestHandler>();
        services.AddTransient<ICommandResponseHandler<AuthenticateCommand, AuthenticateResult>, AuthenticateHandler>();

        return services;
    }
}

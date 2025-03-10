﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Endpoints.Token.Password;
using NCode.Identity.OpenId.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Token;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the token endpoint.
/// </summary>
[PublicAPI]
public static class DefaultTokenEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the token endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddTokenEndpoint(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdEndpointProvider,
            DefaultTokenEndpointHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<ValidateTokenRequestCommand>,
            DefaultValidateTokenRequestHandler>());

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<SelectTokenGrantHandlerCommand, ITokenGrantHandler>,
            DefaultSelectTokenGrantHandlerHandler>();

        // Authorization Code

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ITokenGrantHandler,
            DefaultAuthorizationCodeGrantHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<ValidateTokenGrantCommand<AuthorizationGrant>>,
            DefaultValidateAuthorizationCodeGrantHandler>());

        // Refresh Token

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ITokenGrantHandler,
            DefaultRefreshTokenGrantHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>,
            DefaultValidateRefreshTokenGrantHandler>());

        // Client Credentials

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ITokenGrantHandler,
            DefaultClientCredentialsGrantHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<ValidateTokenGrantCommand<ClientCredentialsGrant>>,
            DefaultValidateClientCredentialsGrantHandler>());

        // Password

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ITokenGrantHandler,
            DefaultPasswordGrantHandler>());

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>,
            DefaultAuthenticatePasswordGrantHandler>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>,
            DefaultValidatePasswordGrantHandler>());

        return serviceCollection;
    }
}

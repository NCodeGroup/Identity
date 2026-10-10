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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Token endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId Token endpoint.
        /// </summary>
        public void AddTokenEndpoint()
        {
            builder.AddEndpoint<DefaultTokenEndpointProvider>();

            builder.AddMessageFactory<TokenRequest>();
            builder.AddMessageFactory<TokenResponse>();

            var serviceCollection = builder.ServiceCollection;

            // Handlers

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<SelectTokenGrantHandlerCommand, ITokenGrantHandler>,
                DefaultSelectTokenGrantHandlerHandler
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenRequestCommand>,
                    DefaultValidateTokenRequestHandler
                >()
            );

            // Authorization Code

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITokenGrantHandler,
                    DefaultAuthorizationCodeGrantHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<AuthorizationGrant>>,
                    DefaultValidateAuthorizationCodeGrantHandler
                >()
            );

            // Refresh Token

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITokenGrantHandler, DefaultRefreshTokenGrantHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>,
                    DefaultValidateRefreshTokenGrantHandler
                >()
            );

            // Device Code

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITokenGrantHandler, DefaultDeviceCodeGrantHandler>()
            );

            // Client Credentials

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITokenGrantHandler,
                    DefaultClientCredentialsGrantHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<ClientCredentialsGrant>>,
                    DefaultValidateClientCredentialsGrantHandler
                >()
            );

            // Password

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITokenGrantHandler, DefaultPasswordGrantHandler>()
            );

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    AuthenticatePasswordGrantCommand,
                    AuthenticateSubjectDisposition
                >,
                DefaultAuthenticatePasswordGrantHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    CreatePasswordGrantSubjectCommand,
                    System.Security.Claims.ClaimsPrincipal
                >,
                DefaultCreatePasswordGrantSubjectHandler
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>,
                    DefaultValidatePasswordGrantHandler
                >()
            );
        }
    }
}

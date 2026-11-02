// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;
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

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<DeviceCodeGrant>>,
                    DefaultValidateDeviceCodeGrantHandler
                >()
            );

            // CIBA (Client-Initiated Backchannel Authentication)

            serviceCollection.TryAddSingleton<
                ICibaTokenResponseService,
                DefaultCibaTokenResponseService
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITokenGrantHandler, DefaultCibaGrantHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<CibaGrant>>,
                    DefaultValidateCibaGrantHandler
                >()
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

            // The password grant is unsupported until a host registers the local-account authentication capability
            // (the NCode.Identity.OpenId.Accounts.Authentication package), which replaces this default handler.
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    AuthenticatePasswordGrantCommand,
                    AuthenticateSubjectDisposition
                >,
                DefaultUnsupportedPasswordGrantHandler
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>,
                    DefaultValidatePasswordGrantHandler
                >()
            );

            // Token Exchange (RFC 8693)

            serviceCollection.TryAddSingleton<
                ISubjectTokenValidator,
                DefaultSubjectTokenValidator
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITokenGrantHandler, DefaultTokenExchangeGrantHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateTokenGrantCommand<TokenExchangeGrant>>,
                    DefaultValidateTokenExchangeGrantHandler
                >()
            );
        }
    }
}

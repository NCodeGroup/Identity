#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Token services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Token services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddTokenServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<ITokenService, DefaultTokenService>();
            serviceCollection.TryAddSingleton<
                IAccessTokenPolicyService,
                DefaultAccessTokenPolicyService
            >();
            serviceCollection.TryAddSingleton<IIssuedTokenValidator, DefaultIssuedTokenValidator>();
            serviceCollection.TryAddSingleton<
                IAuthorizationCodeService,
                DefaultAuthorizationCodeService
            >();

            // Sender-constrained tokens (DPoP — RFC 9449, mutual-TLS — RFC 8705). The nonce and replay stores record
            // into the shared IExpiringKeySet, whose adapter the composition root registers (in-process for a single
            // replica, Valkey for several). The mutual-TLS forwarded-header settings are configured by the host; the
            // server's AddConfiguration binds them from OpenId:MutualTls.
            serviceCollection.AddOptions<MutualTlsForwardingOptions>();
            serviceCollection.TryAddSingleton<
                IProofOfPossessionService,
                DefaultProofOfPossessionService
            >();
            serviceCollection.TryAddSingleton<IDPoPProofValidator, DefaultDPoPProofValidator>();
            serviceCollection.TryAddSingleton<
                IClientCertificateResolver,
                DefaultClientCertificateResolver
            >();
            serviceCollection.TryAddSingleton<IDPoPProofReplayCache, DefaultDPoPProofReplayCache>();
            serviceCollection.TryAddSingleton<IDPoPNonceService, DefaultDPoPNonceService>();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    AuthenticateAccessTokenCommand,
                    AuthenticateAccessTokenDisposition
                >,
                DefaultAuthenticateAccessTokenHandler
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetIdTokenSubjectClaimsCommand>,
                    DefaultGetIdTokenSubjectClaimsHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetIdTokenPayloadClaimsCommand>,
                    DefaultGetIdTokenPayloadClaimsHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
                    DefaultGetAccessTokenSubjectClaimsHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetAccessTokenPayloadClaimsCommand>,
                    DefaultGetAccessTokenPayloadClaimsHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetIdTokenSubjectClaimsCommand>,
                    DefaultGetIdTokenMetadataClaimsHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
                    DefaultGetAccessTokenMetadataClaimsHandler
                >()
            );

            return builder;
        }
    }
}

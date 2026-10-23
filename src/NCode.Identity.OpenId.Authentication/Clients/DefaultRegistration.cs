#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Net;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Servers;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Client services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Client services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddClientServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<IOpenIdClientFactory, DefaultOpenIdClientFactory>();

            serviceCollection.TryAddSingleton<IOpenIdClientResolver, DefaultOpenIdClientResolver>();

            serviceCollection.TryAddSingleton<IClientKeyImporter, DefaultClientKeyImporter>();

            serviceCollection.TryAddSingleton<IClientKeyResolver, DefaultClientKeyResolver>();

            serviceCollection.TryAddSingleton<
                IClientJwtCredentialSelector,
                DefaultClientJwtCredentialSelector
            >();

            // The client jwks_uri fetch targets a client-supplied URL, so it uses a dedicated HTTP client whose
            // connections are pinned away from private/loopback/link-local addresses (SSRF floor) and which never
            // follows redirects, so a redirect cannot bounce the fetch to an internal host.
            serviceCollection
                .AddHttpClient(DefaultClientKeyResolver.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = serviceProvider
                        .GetRequiredService<IOutboundHttpForgeryGuard>()
                        .ConnectAsync,
                    AutomaticDecompression = DecompressionMethods.All,
                });

            serviceCollection.TryAddSingleton<
                IClientAuthenticationService,
                DefaultClientAuthenticationService
            >();

            serviceCollection.TryAddSingleton<
                IClientAssertionReplayCache,
                DefaultClientAssertionReplayCache
            >();

            // Each client-authentication handler below has a descriptor of the same name, so the methods are named
            // without constructing a handler (ADR-0207).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<ClientAuthenticationMethodDescriptor>,
                    DefaultClientAuthenticationMethodDataSource
                >()
            );

            serviceCollection.TryAddSingleton<
                IClientAuthenticationMethodCatalog,
                DefaultClientAuthenticationMethodCatalog
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    ClientSecretBasicClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    ClientSecretPostClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    PrivateKeyJwtClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    ClientSecretJwtClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    TlsClientAuthClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    SelfSignedTlsClientAuthClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientAuthenticationHandler,
                    NoneClientAuthenticationHandler
                >()
            );

            serviceCollection.TryAddSingleton<
                IClientCoherenceValidator,
                DefaultClientCoherenceValidator
            >();

            // The validator runs every registered rule, so a package adds a coherence rule the same way: one
            // TryAddEnumerable registration of its IClientCoherenceRule (ADR-0206).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientCoherenceRule, ResponseAlgorithmCoherenceRule>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientCoherenceRule, RedirectUriKindCoherenceRule>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCoherenceRule,
                    TokenEndpointAuthMethodCoherenceRule
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCoherenceRule,
                    CertificateBoundAccessTokenCoherenceRule
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCoherenceRule,
                    DPoPBoundAccessTokenCoherenceRule
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientCoherenceRule, RegisteredValueCoherenceRule>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientCoherenceRule, IdTokenEncryptionCoherenceRule>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCoherenceRule,
                    BackchannelTokenDeliveryCoherenceRule
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCoherenceRule,
                    RefreshTokenRotationRetryCoherenceRule
                >()
            );

            // A change of the server's settings that is not a write is reported client by client (ADR-0219).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ReconcileServerSettingsCommand>,
                    ClientCoherenceReportHandler
                >()
            );

            return builder;
        }
    }
}

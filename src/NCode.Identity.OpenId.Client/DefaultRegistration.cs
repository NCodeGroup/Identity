// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client.Authentication;
using NCode.Identity.OpenId.Client.Authorization;
using NCode.Identity.OpenId.Client.Discovery;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Keys;
using NCode.Identity.OpenId.Client.Metrics;
using NCode.Identity.OpenId.Client.MutualTls;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.ProofOfPossession;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.OpenId.Client.UserInfo;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register the OpenID client library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="JwtLibrary"/>.</param>
    extension(IServiceBuilder<JwtLibrary> builder)
    {
        /// <summary>
        /// Registers the OpenID client library: provider metadata and key set resolution, client authentication (including mutual-TLS),
        /// the authorization request client (with PAR), the token and token revocation clients, DPoP proofs and nonces, and ID-token and logout-token validation, layered on the
        /// <see cref="JwtLibrary"/>. Every HTTP call is bounded by a total and a per-attempt timeout with bounded retries of safe requests. Logout-token validation looks each token's <c>jti</c> up, and <c>TryRecordHandledAsync</c> records it, in the shared
        /// <c>IExpiringKeySet</c>, whose adapter the composition root registers
        /// (<c>AddSharedStateServices().AddInProcessExpiringKeySet()</c> for one replica, Valkey for several).
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<OpenIdClientLibrary> AddOpenIdClientLibrary()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var serviceCollection = builder.ServiceCollection;

            var newBuilder = builder.NewBuilder<OpenIdClientLibrary>();

            serviceCollection.TryAddSingleton(TimeProvider.System);

            serviceCollection.AddOptions<OpenIdClientOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdClientOptions>,
                    OpenIdClientOptionsValidator
                >()
            );

            // The pipelines count their retries and timeouts on the package's meter (ADR-1197).
            serviceCollection.AddMetrics();
            serviceCollection.TryAddSingleton<ProviderMetrics>();

            // Every HTTP call is bounded by the resilience pipeline, so the client's own timeout is switched off; the
            // default of 100 seconds would silently cut a longer TotalTimeout. See ADR-1336.
            serviceCollection.TryAddSingleton<ProviderResiliencePipelines>();
            serviceCollection.TryAddTransient<ProviderResilienceHandler>();
            serviceCollection
                .AddHttpClient(OpenIdClientDefaults.HttpClientName)
                .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(static () =>
                    new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }
                )
                .AddHttpMessageHandler<ProviderResilienceHandler>();

            serviceCollection.TryAddSingleton<ClientCertificateProvider>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IConfigureOptions<HttpClientFactoryOptions>,
                    MutualTlsHttpClientOptionsSetup
                >()
            );

            serviceCollection.TryAddSingleton<ProviderHttpClient>();
            serviceCollection.TryAddSingleton<ProviderKeySetImporter>();
            serviceCollection.TryAddSingleton<ClientRequestSender>();
            serviceCollection.TryAddSingleton<DPoPNonceCache>();
            serviceCollection.TryAddSingleton<DPoPKeyResolver>();

            serviceCollection.TryAddSingleton<
                IProviderMetadataResolver,
                DefaultProviderMetadataResolver
            >();
            serviceCollection.TryAddSingleton<
                IProviderKeySetResolver,
                DefaultProviderKeySetResolver
            >();
            serviceCollection.TryAddSingleton<
                IClientKeyRingProvider,
                DefaultClientKeyRingProvider
            >();
            serviceCollection.TryAddSingleton<
                IAuthorizationRequestClient,
                DefaultAuthorizationRequestClient
            >();
            serviceCollection.TryAddSingleton<ITokenClient, DefaultTokenClient>();
            serviceCollection.TryAddSingleton<IUserInfoClient, DefaultUserInfoClient>();
            serviceCollection.TryAddSingleton<IDPoPProofFactory, DefaultDPoPProofFactory>();
            serviceCollection.TryAddSingleton<
                ITokenRevocationClient,
                DefaultTokenRevocationClient
            >();
            serviceCollection.TryAddSingleton<ProviderJwtValidator>();
            serviceCollection.TryAddSingleton<IIdTokenValidator, DefaultIdTokenValidator>();
            serviceCollection.TryAddSingleton<ILogoutTokenValidator, DefaultLogoutTokenValidator>();

            serviceCollection.TryAddEnumerable([
                ServiceDescriptor.Singleton<
                    IClientAuthenticator,
                    ClientSecretBasicClientAuthenticator
                >(),
                ServiceDescriptor.Singleton<
                    IClientAuthenticator,
                    ClientSecretPostClientAuthenticator
                >(),
                ServiceDescriptor.Singleton<
                    IClientAuthenticator,
                    PrivateKeyJwtClientAuthenticator
                >(),
                ServiceDescriptor.Singleton<
                    IClientAuthenticator,
                    TlsClientAuthClientAuthenticator
                >(),
                ServiceDescriptor.Singleton<
                    IClientAuthenticator,
                    SelfSignedTlsClientAuthClientAuthenticator
                >(),
            ]);

            return newBuilder;
        }
    }
}

#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using Microsoft.Extensions.Options;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client.Authentication;
using NCode.Identity.OpenId.Client.Discovery;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Keys;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Registration;

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
        /// Registers the OpenID client library: provider metadata and key set resolution, client authentication,
        /// the token and token revocation clients, and ID-token and logout-token validation, layered on the
        /// <see cref="JwtLibrary"/>. Logout-token validation records each token's <c>jti</c> in the shared
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

            serviceCollection
                .AddHttpClient(OpenIdClientDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(static () =>
                    new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }
                );

            serviceCollection.TryAddSingleton<ProviderHttpClient>();
            serviceCollection.TryAddSingleton<ProviderKeySetImporter>();
            serviceCollection.TryAddSingleton<ClientRequestSender>();

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
            serviceCollection.TryAddSingleton<ITokenClient, DefaultTokenClient>();
            serviceCollection.TryAddSingleton<
                ITokenRevocationClient,
                DefaultTokenRevocationClient
            >();
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
            ]);

            return newBuilder;
        }
    }
}

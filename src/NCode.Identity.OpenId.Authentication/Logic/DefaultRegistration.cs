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

using System.Net;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Logic services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Logic services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddLogicServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IAuthorizationCodeService,
                DefaultAuthorizationCodeService
            >();
            serviceCollection.TryAddSingleton<
                Persistence.IPersistedPayloadSerializer,
                Persistence.DefaultPersistedPayloadSerializer
            >();
            serviceCollection.TryAddSingleton<
                IPersistedGrantService,
                DefaultPersistedGrantService
            >();
            serviceCollection.TryAddSingleton<IClientScopeService, DefaultClientScopeService>();
            serviceCollection.TryAddSingleton<
                IScopeDescriptionService,
                DefaultScopeDescriptionService
            >();
            serviceCollection.TryAddSingleton<
                IResourceServerCredentialResolver,
                DefaultResourceServerCredentialResolver
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ICodeChallengeMethod, PlainCodeChallengeMethod>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ICodeChallengeMethod, S256CodeChallengeMethod>()
            );
            serviceCollection.TryAddSingleton<
                ICodeChallengeMethodCollectionProvider,
                DefaultCodeChallengeMethodCollectionProvider
            >();

            serviceCollection.TryAddSingleton<
                Sessions.ISessionService,
                Sessions.DefaultSessionService
            >();

            serviceCollection.TryAddSingleton<
                Sessions.ISessionEvictionPolicy,
                Sessions.DefaultOldestFirstSessionEvictionPolicy
            >();

            // OnSessionTerminatedCommand is the session-termination side-effect extension point: applications add
            // handlers (for example, front-channel logout or custom auditing) alongside these defaults, ordered by
            // mediator priority.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    NCode.Mediator.ICommandHandler<Sessions.OnSessionTerminatedCommand>,
                    Sessions.DefaultRevokeGrantsOnSessionTerminatedHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    NCode.Mediator.ICommandHandler<Sessions.OnSessionTerminatedCommand>,
                    Sessions.DefaultBackChannelLogoutOnSessionTerminatedHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    NCode.Mediator.ICommandHandler<Sessions.OnSessionTerminatedCommand>,
                    Sessions.DefaultAuditSessionTerminatedHandler
                >()
            );

            serviceCollection.TryAddSingleton<
                Sessions.ISessionStateService,
                Sessions.DefaultSessionStateService
            >();

            serviceCollection.TryAddSingleton<
                Sessions.IFrontChannelLogoutService,
                Sessions.DefaultFrontChannelLogoutService
            >();

            serviceCollection.TryAddSingleton<
                Consent.IConsentService,
                Consent.DefaultConsentService
            >();

            serviceCollection.AddHttpClient();
            serviceCollection.TryAddSingleton<
                IOutboundHttpForgeryGuard,
                OutboundHttpForgeryGuard
            >();
            serviceCollection.TryAddSingleton<
                IOutboundHttpContentReader,
                DefaultOutboundHttpContentReader
            >();
            serviceCollection.TryAddSingleton<
                Sessions.IBackChannelLogoutNotifier,
                Sessions.DefaultBackChannelLogoutNotifier
            >();

            // Back-channel logout POSTs to the relying party's client-registered backchannel_logout_uri, so it uses a
            // dedicated SSRF-guarded HTTP client whose connections are pinned away from private/loopback/link-local
            // addresses and which never follows redirects, so a registered internal URL cannot turn the OP into a
            // confused deputy.
            serviceCollection
                .AddHttpClient(Sessions.BackChannelLogoutDeliveryJobHandler.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = serviceProvider
                        .GetRequiredService<IOutboundHttpForgeryGuard>()
                        .ConnectAsync,
                    AutomaticDecompression = DecompressionMethods.All,
                });

            // Back-channel logout delivery runs as a durable, retryable dispatch job; the handler performs the POST.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<
                    Dispatch.IDispatchJobHandler<Sessions.BackChannelLogoutDeliveryJob>,
                    Sessions.BackChannelLogoutDeliveryJobHandler
                >()
            );

            return builder;
        }
    }
}

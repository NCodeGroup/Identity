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

using System.Net;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides extension methods to configure the end-user session, session-termination, and logout services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the session service, its eviction policy and termination handlers, the session-state service,
        /// front- and back-channel logout delivery, and the hosted service that terminates the sessions that ended
        /// while no request used them.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddSessionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<ISessionService, DefaultSessionService>();
            serviceCollection.TryAddSingleton<
                ISessionEvictionPolicy,
                DefaultOldestFirstSessionEvictionPolicy
            >();

            // OnSessionTerminatedCommand is the session-termination side-effect extension point: applications add
            // handlers (for example, front-channel logout or custom auditing) alongside these defaults, ordered by
            // mediator priority.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<OnSessionTerminatedCommand>,
                    DefaultRevokeGrantsOnSessionTerminatedHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<OnSessionTerminatedCommand>,
                    DefaultBackChannelLogoutOnSessionTerminatedHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<OnSessionTerminatedCommand>,
                    DefaultAuditSessionTerminatedHandler
                >()
            );

            serviceCollection.TryAddSingleton<ISessionStateService, DefaultSessionStateService>();
            serviceCollection.TryAddSingleton<
                IFrontChannelLogoutService,
                DefaultFrontChannelLogoutService
            >();
            serviceCollection.TryAddSingleton<
                IBackChannelLogoutNotifier,
                DefaultBackChannelLogoutNotifier
            >();

            // Back-channel logout POSTs to the relying party's client-registered backchannel_logout_uri, so it uses a
            // dedicated SSRF-guarded HTTP client whose connections are pinned away from private/loopback/link-local
            // addresses and which never follows redirects, so a registered internal URL cannot turn the OP into a
            // confused deputy.
            serviceCollection
                .AddHttpClient(BackChannelLogoutDeliveryJobHandler.HttpClientName)
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
                    IDispatchJobHandler<BackChannelLogoutDeliveryJob>,
                    BackChannelLogoutDeliveryJobHandler
                >()
            );

            // A session that ends while no request uses it is terminated in the background, once (ADR-0223).
            serviceCollection.AddOptions<EndedSessionTerminationOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<EndedSessionTerminationOptions>,
                    EndedSessionTerminationOptionsValidator
                >()
            );
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddHostedService<EndedSessionTerminationService>();

            return builder;
        }
    }
}

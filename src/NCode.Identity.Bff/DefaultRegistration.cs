// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.Events;
using NCode.Identity.OpenId.Client;
using NCode.Registration;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register the backend-for-frontend (BFF) library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="OpenIdClientLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdClientLibrary> builder)
    {
        /// <summary>
        /// Registers the BFF library: its options, the frontends (the selection rules, the catalog and the routing
        /// policy that keeps a request's endpoints to its frontend), the session store and manager, the audit
        /// recorder and the cookie writer, layered on the <see cref="OpenIdClientLibrary"/>. The composition root
        /// registers the shared-state adapters
        /// (<c>AddSharedStateServices().AddInProcessExpiringKeySet().AddInProcessExpiringValueSet().AddInProcessExpiringIndexSet()</c>
        /// for one replica, Valkey for several).
        /// </summary>
        /// <param name="events">The <see cref="IServiceBuilder{T}"/> for <see cref="EventsLibrary"/> that proves the
        /// event services are registered.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<BffLibrary> AddBffLibrary(IServiceBuilder<EventsLibrary> events)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(events);

            var serviceCollection = builder.ServiceCollection;

            var newBuilder = builder.NewBuilder<BffLibrary>();

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddHttpContextAccessor();

            serviceCollection.AddOptions<BffOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<BffOptions>, BffOptionsValidator>()
            );

            // The BFF's instruments are created through the IMeterFactory (ADR-1197).
            serviceCollection.AddMetrics();
            serviceCollection.TryAddSingleton<BffMetrics>();

            serviceCollection.AddRouting();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffFrontendSelector, HostBffFrontendSelector>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffFrontendSelector, PathBffFrontendSelector>()
            );
            serviceCollection.TryAddSingleton<BffFrontendCatalog>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<MatcherPolicy, BffFrontendMatcherPolicy>()
            );

            serviceCollection.TryAddSingleton<IBffSessionStore, DefaultBffSessionStore>();
            serviceCollection.TryAddSingleton<BffInstrumentedSessionStore>();
            serviceCollection.TryAddSingleton<IBffSessionManager, DefaultBffSessionManager>();
            serviceCollection.TryAddSingleton<IBffAuditRecorder, DefaultBffAuditRecorder>();
            serviceCollection.TryAddSingleton<BffCookies>();
            serviceCollection.TryAddSingleton<BffSessionResolver>();
            serviceCollection.TryAddSingleton<BffLoginTransactionService>();
            serviceCollection.TryAddSingleton<BffLoginHandler>();
            serviceCollection.TryAddSingleton<BffCallbackHandler>();
            serviceCollection.TryAddSingleton<BffUserHandler>();
            serviceCollection.TryAddSingleton<BffLogoutService>();
            serviceCollection.TryAddSingleton<BffLogoutHandler>();
            serviceCollection.TryAddSingleton<BffBackChannelLogoutHandler>();
            serviceCollection.TryAddSingleton<BffJwksHandler>();

            serviceCollection.TryAddSingleton<BffRefreshLocks>();
            serviceCollection.TryAddSingleton<BffTokenContexts>();
            serviceCollection.TryAddSingleton<IBffUserTokenService, DefaultBffUserTokenService>();
            serviceCollection.TryAddSingleton<BffGrantClient>();
            serviceCollection.TryAddSingleton<
                IBffClientTokenService,
                DefaultBffClientTokenService
            >();
            serviceCollection.TryAddSingleton<
                IBffExchangeTokenService,
                DefaultBffExchangeTokenService
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffUserTokenRequirement>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffClientTokenRequirement>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffUserOrClientTokenRequirement>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffExchangeTokenRequirement>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffNoneTokenRequirement>()
            );
            serviceCollection.TryAddSingleton<IBffTokenAccessor, DefaultBffTokenAccessor>();

            serviceCollection.AddHttpForwarder();
            serviceCollection.TryAddSingleton<BffRemoteApiInvokerFactory>();
            serviceCollection.TryAddSingleton<BffRemoteApiRegistry>();
            serviceCollection.TryAddSingleton<BffRemoteApiHandler>();

            serviceCollection
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, BffAuthenticationHandler>(
                    BffDefaults.AuthenticationScheme,
                    null,
                    null
                );
            serviceCollection.PostConfigure<AuthenticationOptions>(o =>
                o.DefaultScheme ??= BffDefaults.AuthenticationScheme
            );
            serviceCollection.AddAuthorization();
            serviceCollection.AddHostedService<BffStartupCheck>();
            serviceCollection.AddHostedService<BffEndpointStartupCheck>();

            return newBuilder;
        }
    }
}

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
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
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
        /// Registers the BFF library: its options, the session store and manager, the audit recorder and the cookie
        /// writer, layered on the <see cref="OpenIdClientLibrary"/>. The composition root registers the shared-state
        /// adapters (<c>AddSharedStateServices().AddInProcessExpiringKeySet().AddInProcessExpiringValueSet().AddInProcessExpiringIndexSet()</c>
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

            serviceCollection.TryAddSingleton<IBffSessionStore, DefaultBffSessionStore>();
            serviceCollection.TryAddSingleton<IBffSessionManager, DefaultBffSessionManager>();
            serviceCollection.TryAddSingleton<IBffAuditRecorder, DefaultBffAuditRecorder>();
            serviceCollection.TryAddSingleton<BffCookies>();
            serviceCollection.TryAddSingleton<BffSessionResolver>();
            serviceCollection.TryAddSingleton<BffClientRegistrationProvider>();
            serviceCollection.TryAddSingleton<BffLoginTransactionService>();
            serviceCollection.TryAddSingleton<BffLoginHandler>();
            serviceCollection.TryAddSingleton<BffCallbackHandler>();
            serviceCollection.TryAddSingleton<BffUserHandler>();
            serviceCollection.TryAddSingleton<BffLogoutService>();
            serviceCollection.TryAddSingleton<BffLogoutHandler>();
            serviceCollection.TryAddSingleton<BffBackChannelLogoutHandler>();

            serviceCollection.TryAddSingleton<BffRefreshLocks>();
            serviceCollection.TryAddSingleton<IBffUserTokenService, DefaultBffUserTokenService>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBffTokenRequirement, BffUserTokenRequirement>()
            );
            serviceCollection.TryAddSingleton<IBffTokenAccessor, DefaultBffTokenAccessor>();

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

            return newBuilder;
        }
    }
}

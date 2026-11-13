// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Authentication.OutboundHttp.Facades;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides extension methods to configure the shared, SSRF-guarded, bounded outbound HTTP services used by every
/// server-initiated fetch of, or delivery to, a client-supplied URL.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the outbound HTTP forgery guard, the capped response-body reader, and the one guarded client
        /// with its timeouts and bounded retries.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddOutboundHttpServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddHttpClient();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IOutboundHttpForgeryGuard,
                DefaultOutboundHttpForgeryGuard
            >();
            serviceCollection.TryAddSingleton<IOutboundHostResolver, DefaultOutboundHostResolver>();
            serviceCollection.TryAddSingleton<IOutboundHostChecker, DefaultOutboundHostChecker>();
            serviceCollection.TryAddSingleton<
                IOutboundHttpContentReader,
                DefaultOutboundHttpContentReader
            >();

            serviceCollection.AddOptions<OutboundHttpOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OutboundHttpOptions>,
                    OutboundHttpOptionsValidator
                >()
            );
            // The pipelines count their retries and timeouts on the package's meter (ADR-1197).
            serviceCollection.AddMetrics();
            serviceCollection.TryAddSingleton<AuthenticationMetrics>();
            serviceCollection.TryAddSingleton<GuardedResiliencePipelines>();
            serviceCollection.TryAddTransient<GuardedResilienceHandler>();

            // This one client serves every fetch of, and delivery to, a caller-supplied URL (ADR-1315). Its
            // connections are pinned away from private/loopback/link-local addresses and it never follows redirects,
            // so a registered internal URL cannot turn the OP into a confused deputy; its handler bounds each attempt
            // and the whole call with a Polly.Core pipeline (ADR-1385), and retries a failed attempt a bounded number
            // of times. The pipeline is the only time bound, so the client's own timeout is switched off; its default
            // of 100 seconds would silently cut a longer TotalTimeout.
            serviceCollection
                .AddHttpClient(GuardedHttpClient.Name)
                .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = serviceProvider
                        .GetRequiredService<IOutboundHttpForgeryGuard>()
                        .ConnectAsync,
                    AutomaticDecompression = DecompressionMethods.All,
                })
                .AddHttpMessageHandler<GuardedResilienceHandler>();

            return builder;
        }
    }
}

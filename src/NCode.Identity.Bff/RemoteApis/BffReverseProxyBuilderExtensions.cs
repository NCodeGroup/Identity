// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Provides extension methods for <see cref="IReverseProxyBuilder"/> to make YARP-configured routes BFF remote APIs.
/// </summary>
[PublicAPI]
public static class BffReverseProxyBuilderExtensions
{
    /// <param name="builder">The <see cref="IReverseProxyBuilder"/> to configure.</param>
    extension(IReverseProxyBuilder builder)
    {
        /// <summary>
        /// Makes each YARP route whose metadata names a <see cref="BffReverseProxyMetadataKeys.TokenRequirement"/> a BFF
        /// remote API, and keeps the BFF's own cookies away from every upstream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A remote API route loses the browser's <c>Cookie</c>, <c>Authorization</c>, <c>DPoP</c> and anti-forgery
        /// headers. Its token, requested for the optional <see cref="BffReverseProxyMetadataKeys.Resource"/> and
        /// <see cref="BffReverseProxyMetadataKeys.Scope"/>, is resolved before anything is forwarded: a failure answers
        /// the same problem as <c>UseBff()</c>. The token is attached with its DPoP proof when it is DPoP-bound, a
        /// resource server's DPoP nonce challenge is retried once, and the upstream response loses every
        /// <c>Set-Cookie</c>. Forwarding headers follow the route's own YARP transforms.
        /// </para>
        /// <para>
        /// On every other route the BFF's session and login cookies are removed from the request, and an upstream
        /// <c>Set-Cookie</c> for either is dropped.
        /// </para>
        /// <para>
        /// The method wraps the last registered <see cref="IForwarderHttpClientFactory"/>, so call it after
        /// <c>ConfigureHttpClient()</c> and after registering a custom factory; the proxy refuses to start otherwise.
        /// Mark the routes as BFF APIs with <c>MapReverseProxy().AsBffRemoteApis()</c>.
        /// </para>
        /// </remarks>
        /// <returns>The <see cref="IReverseProxyBuilder"/> so that additional calls can be chained.</returns>
        /// <exception cref="InvalidOperationException">No <see cref="IForwarderHttpClientFactory"/> is registered.</exception>
        public IReverseProxyBuilder AddBffTransforms()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var serviceCollection = builder.Services;
            var isAdded = serviceCollection.Any(descriptor =>
                descriptor.ServiceType == typeof(ITransformProvider)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationType == typeof(BffTransformProvider)
            );
            if (isAdded)
                return builder;

            DecorateClientFactory(serviceCollection);
            serviceCollection.AddSingleton<ITransformProvider, BffTransformProvider>();
            return builder;
        }
    }

    private static void DecorateClientFactory(IServiceCollection serviceCollection)
    {
        var index = -1;
        for (var i = serviceCollection.Count - 1; i >= 0 && index < 0; i--)
        {
            var descriptor = serviceCollection[i];
            if (
                descriptor.ServiceType == typeof(IForwarderHttpClientFactory)
                && !descriptor.IsKeyedService
            )
            {
                index = i;
            }
        }

        if (index < 0)
        {
            throw new InvalidOperationException(
                "AddBffTransforms() needs the IForwarderHttpClientFactory that AddReverseProxy() registers."
            );
        }

        var inner = serviceCollection[index];
        serviceCollection[index] = ServiceDescriptor.Describe(
            typeof(IForwarderHttpClientFactory),
            serviceProvider =>
                ActivatorUtilities.CreateInstance<BffForwarderHttpClientFactory>(
                    serviceProvider,
                    CreateInner(serviceProvider, inner)
                ),
            inner.Lifetime
        );
    }

    private static IForwarderHttpClientFactory CreateInner(
        IServiceProvider serviceProvider,
        ServiceDescriptor descriptor
    )
    {
        var instance =
            descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(
                serviceProvider,
                descriptor.ImplementationType
                    ?? throw new InvalidOperationException(
                        "The IForwarderHttpClientFactory registration has no implementation."
                    )
            );

        return (IForwarderHttpClientFactory)instance;
    }
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Provides extension methods that opt endpoints in to HTTP logging of their bodies, and that mark endpoints whose
/// bodies HTTP logging never writes.
/// </summary>
[PublicAPI]
public static class HttpBodyLoggingEndpointConventionBuilderExtensions
{
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Opts the endpoint or route group in to HTTP logging of its request body. An endpoint that has
        /// confidential bodies refuses the opt-in when its endpoints are built.
        /// </summary>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithRequestBodyLogging()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.WithMetadata(HttpBodyLoggingMetadata.RequestBody);
            builder.Finally(RefuseConfidentialBodies);
            return builder;
        }

        /// <summary>
        /// Opts the endpoint or route group in to HTTP logging of its response body. An endpoint that has
        /// confidential bodies refuses the opt-in when its endpoints are built.
        /// </summary>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithResponseBodyLogging()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.WithMetadata(HttpBodyLoggingMetadata.ResponseBody);
            builder.Finally(RefuseConfidentialBodies);
            return builder;
        }

        /// <summary>
        /// Marks the endpoint or route group as one whose request and response bodies carry credentials or personal
        /// data, so HTTP logging never writes either body and an opt-in to body logging is refused.
        /// </summary>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithConfidentialBodies()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.WithMetadata(ConfidentialBodyMetadata.Instance);
            return builder;
        }
    }

    private static void RefuseConfidentialBodies(EndpointBuilder endpointBuilder)
    {
        if (!endpointBuilder.Metadata.OfType<ConfidentialBodyMetadata>().Any())
        {
            return;
        }

        throw new InvalidOperationException(
            $"The endpoint '{endpointBuilder.DisplayName}' has confidential bodies, so HTTP logging never writes its request or response body: remove WithRequestBodyLogging() and WithResponseBodyLogging() from it."
        );
    }
}

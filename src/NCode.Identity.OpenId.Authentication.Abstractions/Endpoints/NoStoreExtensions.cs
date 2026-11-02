// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the extension methods that mark a response as non-cacheable, so that no client or intermediary stores a
/// response that can carry secret material.
/// </summary>
// See ADR-1345.
[PublicAPI]
public static class NoStoreExtensions
{
    private const string NoStoreCacheControl = "no-store, no-cache, max-age=0";
    private const string NoCachePragma = "no-cache";

    /// <param name="response">The <see cref="HttpResponse"/> instance.</param>
    extension(HttpResponse response)
    {
        /// <summary>
        /// Marks the response so that no client or intermediary stores it: emits
        /// <c>Cache-Control: no-store, no-cache, max-age=0</c> and the legacy <c>Pragma: no-cache</c>. The headers are
        /// assigned, so a value that another component wrote is replaced.
        /// </summary>
        public void SetNoStore()
        {
            ArgumentNullException.ThrowIfNull(response);

            response.Headers.CacheControl = NoStoreCacheControl;
            response.Headers.Pragma = NoCachePragma;
        }
    }

    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Adds a filter that marks every response of the endpoint with <c>SetNoStore</c> before the endpoint runs, for
        /// an endpoint whose response can carry secret material.
        /// </summary>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithNoStore() => builder.AddEndpointFilter(NoStoreEndpointFilter.Instance);
    }
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the extension method that marks every response of an endpoint as non-cacheable, for an endpoint whose
/// response can carry secret material.
/// </summary>
// See ADR-1345.
[PublicAPI]
public static class NoStoreEndpointConventionBuilderExtensions
{
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
        public TBuilder WithNoStore()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.AddEndpointFilter(NoStoreEndpointFilter.Instance);
        }
    }
}

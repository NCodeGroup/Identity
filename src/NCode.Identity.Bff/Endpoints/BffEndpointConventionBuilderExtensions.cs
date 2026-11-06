// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Provides the ability to mark minimal-API endpoints as local backend-for-frontend (BFF) APIs or pages.
/// </summary>
[PublicAPI]
public static class BffEndpointConventionBuilderExtensions
{
    /// <typeparam name="TBuilder">The type of the endpoint convention builder.</typeparam>
    /// <param name="builder">The endpoint convention builder to mark.</param>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Marks the endpoint as a local BFF API by adding a <see cref="BffApiAttribute"/> to its metadata.
        /// </summary>
        /// <remarks>
        /// The marking requires the anti-forgery header, passes the Fetch Metadata check, requires a BFF session,
        /// and answers a challenge with a <c>401</c> problem and a forbid with a <c>403</c> problem.
        /// Controllers use the <see cref="BffApiAttribute"/> attribute instead.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public TBuilder AsBffApi()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var attribute = new BffApiAttribute();
            return builder.WithMetadata(attribute);
        }

        /// <summary>
        /// Marks the endpoint as a BFF page by adding a <see cref="BffPageAttribute"/> to its metadata.
        /// </summary>
        /// <remarks>
        /// The marking lets a BFF session authenticate the request on <c>GET</c> and <c>HEAD</c> only, requires no
        /// anti-forgery header, and answers a challenge with a redirect to the BFF login.
        /// Controllers use the <see cref="BffPageAttribute"/> attribute instead.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public TBuilder AsBffPage()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var attribute = new BffPageAttribute();
            return builder.WithMetadata(attribute);
        }

        /// <summary>
        /// Limits the endpoint to the named BFF frontends by adding one <see cref="BffFrontendAttribute"/> per name
        /// to its metadata.
        /// </summary>
        /// <remarks>
        /// A request that selects another frontend does not match the endpoint, so routing answers <c>404</c>.
        /// Controllers use the <see cref="BffFrontendAttribute"/> attribute instead.
        /// </remarks>
        /// <param name="names">The names of the frontends that the endpoint serves.</param>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public TBuilder RequireBffFrontend(params IEnumerable<string> names)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(names);

            string[] buffered = [.. names];
            if (buffered.Length == 0)
            {
                throw new ArgumentException(
                    "At least one frontend name is required.",
                    nameof(names)
                );
            }

            var attributes = buffered
                .Select(name => new BffFrontendAttribute(name))
                .ToArray<object>();
            return builder.WithMetadata(attributes);
        }
    }
}

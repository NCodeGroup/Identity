// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Provides the ability to mark Razor Components endpoints as backend-for-frontend (BFF) endpoints.
/// </summary>
[PublicAPI]
public static class BffRazorComponentsEndpointConventionBuilderExtensions
{
    /// <param name="builder">The Razor Components endpoint convention builder to mark.</param>
    extension(RazorComponentsEndpointConventionBuilder builder)
    {
        /// <summary>
        /// Marks the Razor Components endpoints as BFF endpoints.
        /// </summary>
        /// <remarks>
        /// Component pages are BFF pages (an <c>[AllowAnonymous]</c> component opts out of authorization), the
        /// interactive server hub is a BFF hub, and Blazor's own framework endpoints stay outside the BFF. The host
        /// refuses to start when <c>AddBffBlazorLibrary()</c> is missing.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public RazorComponentsEndpointConventionBuilder AsBffComponents()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Add(BffComponentsConvention.Apply);
            return builder;
        }
    }
}

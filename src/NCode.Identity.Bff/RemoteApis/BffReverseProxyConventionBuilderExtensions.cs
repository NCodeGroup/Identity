// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using NCode.Identity.Bff.Endpoints;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Provides extension methods for <see cref="ReverseProxyConventionBuilder"/> to mark YARP routes as BFF APIs.
/// </summary>
[PublicAPI]
public static class BffReverseProxyConventionBuilderExtensions
{
    /// <param name="builder">The <see cref="ReverseProxyConventionBuilder"/> that <c>MapReverseProxy()</c> returned.</param>
    extension(ReverseProxyConventionBuilder builder)
    {
        /// <summary>
        /// Marks each YARP route whose metadata names a <see cref="BffReverseProxyMetadataKeys.TokenRequirement"/> as a
        /// BFF API, as <c>AsBffApi()</c> does, so that it requires a BFF session and the anti-forgery header. Other routes
        /// are left as they are.
        /// </summary>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public ReverseProxyConventionBuilder AsBffRemoteApis()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.ConfigureEndpoints(
                (endpoint, route) =>
                {
                    if (BffReverseProxyRoutes.IsRemoteApi(route))
                        endpoint.AsBffApi();
                }
            );
        }
    }
}

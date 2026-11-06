// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Provides extension methods for <see cref="IHttpClientBuilder"/> to call the frontend's BFF APIs from WebAssembly.
/// </summary>
[PublicAPI]
public static class BffBlazorClientHttpClientBuilderExtensions
{
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Adds a handler so that a client of the frontend's BFF APIs sends the anti-forgery header, and a <c>401</c>
        /// re-probes the session. Requests to another origin are sent unchanged.
        /// </summary>
        /// <returns>The <see cref="IHttpClientBuilder"/> so that additional calls can be chained.</returns>
        public IHttpClientBuilder AddBffAntiforgeryHeader()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.AddHttpMessageHandler<BffAntiforgeryHeaderHandler>();
        }
    }
}

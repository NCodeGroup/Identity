// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Registers the host hardening that turns off Kestrel's <c>Server</c> response header.
/// </summary>
// See ADR-1511.
[PublicAPI]
public static class ServerHeaderRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Turns off the <c>Server</c> header Kestrel adds to every response, in every environment, so no response
        /// names the server software.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddServerHeaderSuppression()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.Configure<KestrelServerOptions>(options =>
                options.AddServerHeader = false
            );

            return serviceCollection;
        }
    }
}

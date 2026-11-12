// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.Registration;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides extension methods to read <see cref="ProxyClientCertificateOptions"/> from configuration.
/// </summary>
[PublicAPI]
public static class ProxyClientCertificateConfiguration
{
    /// <param name="configuration">The <see cref="IConfiguration"/> to read the proxy client certificate options from.</param>
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// Binds <see cref="ProxyClientCertificateOptions"/> from the <c>ProxyClientCertificate</c> configuration
        /// section. The section is read when the host configures Kestrel, before the service provider exists, so it is
        /// bound strictly right away.
        /// </summary>
        /// <returns>The bound <see cref="ProxyClientCertificateOptions"/>.</returns>
        /// <exception cref="InvalidOperationException">The section contains a key that maps to no option, or a value
        /// that is not valid for its option.</exception>
        public ProxyClientCertificateOptions GetProxyClientCertificateOptions()
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var options = new ProxyClientCertificateOptions();
            var section = configuration.GetSection(ProxyClientCertificateOptions.DefaultSectionName);
            section.BindStrictly(options);

            return options;
        }
    }
}

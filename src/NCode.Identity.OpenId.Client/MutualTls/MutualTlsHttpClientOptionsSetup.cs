// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Client.MutualTls;

/// <summary>
/// Gives every <c>IHttpClientFactory</c> client whose name starts with
/// <see cref="OpenIdClientDefaults.MutualTlsHttpClientNamePrefix"/> a primary handler that presents the selected TLS
/// client certificate.
/// </summary>
internal class MutualTlsHttpClientOptionsSetup(ClientCertificateProvider certificateProvider)
    : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    private ClientCertificateProvider CertificateProvider { get; } = certificateProvider;

    /// <inheritdoc />
    public void Configure(string? name, HttpClientFactoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        const string prefix = OpenIdClientDefaults.MutualTlsHttpClientNamePrefix;
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal))
            return;

        var thumbprint = name[prefix.Length..];
        options.HttpMessageHandlerBuilderActions.Add(builder =>
            builder.PrimaryHandler = CertificateProvider.CreatePrimaryHandler(thumbprint)
        );
    }

    /// <inheritdoc />
    public void Configure(HttpClientFactoryOptions options) =>
        Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
}

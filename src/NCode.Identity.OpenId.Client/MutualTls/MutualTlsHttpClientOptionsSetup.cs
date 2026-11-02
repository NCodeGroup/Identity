// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Client.Http;

namespace NCode.Identity.OpenId.Client.MutualTls;

/// <summary>
/// Gives every <c>IHttpClientFactory</c> client whose name starts with
/// <see cref="OpenIdClientDefaults.MutualTlsHttpClientNamePrefix"/> a primary handler that presents the selected TLS
/// client certificate. The client is bounded like the named client: a resilience handler applies the timeouts and
/// retries of <c>OpenIdClientOptions</c>, and the client's own timeout is switched off.
/// </summary>
// See ADR-1336.
internal class MutualTlsHttpClientOptionsSetup(
    ClientCertificateProvider certificateProvider,
    ProviderResiliencePipelines resiliencePipelines
) : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    private ClientCertificateProvider CertificateProvider { get; } = certificateProvider;
    private ProviderResiliencePipelines ResiliencePipelines { get; } = resiliencePipelines;

    /// <inheritdoc />
    public void Configure(string? name, HttpClientFactoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        const string prefix = OpenIdClientDefaults.MutualTlsHttpClientNamePrefix;
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal))
            return;

        var thumbprint = name[prefix.Length..];
        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            builder.PrimaryHandler = CertificateProvider.CreatePrimaryHandler(thumbprint);

            var resilienceHandler = new ProviderResilienceHandler(ResiliencePipelines);
            builder.AdditionalHandlers.Add(resilienceHandler);
        });
        options.HttpClientActions.Add(static client => client.Timeout = Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void Configure(HttpClientFactoryOptions options) =>
        Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
}

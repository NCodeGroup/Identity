#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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

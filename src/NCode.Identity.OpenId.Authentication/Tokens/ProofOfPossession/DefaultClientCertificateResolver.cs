#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IClientCertificateResolver"/> abstraction: it reads the
/// certificate negotiated directly on the connection and, when a trusted forwarded header is configured via
/// <see cref="MutualTlsForwardingOptions"/>, the certificate that header carries — so the server works both with
/// direct TLS and behind a TLS-terminating reverse proxy, load balancer, or service mesh without any proxy-specific
/// code.
/// </summary>
internal sealed class DefaultClientCertificateResolver(IOptions<MutualTlsForwardingOptions> options)
    : IClientCertificateResolver
{
    private MutualTlsForwardingOptions Options { get; } = options.Value;

    /// <inheritdoc />
    public async ValueTask<X509Certificate2?> ResolveCertificateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var headerName = Options.CertificateHeaderName;
        if (
            !string.IsNullOrEmpty(headerName)
            && httpContext.Request.Headers.TryGetValue(headerName, out var headerValues)
        )
        {
            var headerValue = headerValues.ToString();
            if (
                !string.IsNullOrEmpty(headerValue)
                && TryParseCertificate(headerValue, Options.Format, out var forwarded)
            )
            {
                return forwarded;
            }
        }

        var certificate = httpContext.Connection.ClientCertificate;
        if (certificate is not null)
            return certificate;

        return await httpContext.Connection.GetClientCertificateAsync(cancellationToken);
    }

    private static bool TryParseCertificate(
        string headerValue,
        MutualTlsClientCertificateFormat format,
        out X509Certificate2? certificate
    )
    {
        certificate = null;
        try
        {
            switch (format)
            {
                case MutualTlsClientCertificateFormat.Base64Der:
                    certificate = X509CertificateLoader.LoadCertificate(
                        Convert.FromBase64String(headerValue)
                    );
                    return true;

                case MutualTlsClientCertificateFormat.Pem:
                    certificate = X509Certificate2.CreateFromPem(headerValue);
                    return true;

                case MutualTlsClientCertificateFormat.UrlEncodedPem:
                    certificate = X509Certificate2.CreateFromPem(
                        Uri.UnescapeDataString(headerValue)
                    );
                    return true;

                case MutualTlsClientCertificateFormat.EnvoyXfcc:
                    return TryParseXfcc(headerValue, out certificate);

                default:
                    return false;
            }
        }
        catch (Exception exception)
            when (exception is FormatException or CryptographicException or ArgumentException)
        {
            certificate = null;
            return false;
        }
    }

    private static bool TryParseXfcc(string headerValue, out X509Certificate2? certificate)
    {
        certificate = null;

        // XFCC is a comma-separated list of proxy elements; the first is the one closest to the client. Each element
        // is a ';'-separated set of key=value pairs where 'Cert' carries URL-encoded PEM, optionally quoted.
        var firstElement = headerValue.Split(',', 2)[0];
        foreach (var pair in firstElement.Split(';'))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = pair[..separatorIndex].Trim();
            if (!key.Equals("Cert", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = pair[(separatorIndex + 1)..].Trim().Trim('"');
            certificate = X509Certificate2.CreateFromPem(Uri.UnescapeDataString(value));
            return true;
        }

        return false;
    }
}

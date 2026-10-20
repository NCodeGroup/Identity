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

using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of the <see cref="IClientCertificateResolver"/> abstraction. When a trusted
/// forwarded header is configured through <see cref="MutualTlsForwardingOptions"/>, that header is the only source:
/// its value is decoded by the <see cref="IClientCertificateHeaderDecoder"/> registered for the configured format, and
/// a value it cannot decode fails the request. Otherwise the certificate negotiated directly on the connection is used.
/// The result is resolved once per request, and the certificates decoded from a header are disposed with the request.
/// </summary>
// See ADR-0172.
internal sealed class DefaultClientCertificateResolver(
    IOptions<MutualTlsForwardingOptions> options,
    IEnumerable<IClientCertificateHeaderDecoder> decoders,
    ILogger<DefaultClientCertificateResolver> logger
) : IClientCertificateResolver
{
    private const string ItemKey =
        "NCode.Identity.OpenId.Authentication.PresentedClientCertificate";

    private static object NoCertificate { get; } = new();

    private MutualTlsForwardingOptions Options { get; } = options.Value;

    private FrozenDictionary<string, IClientCertificateHeaderDecoder> Decoders { get; } =
        CreateDecoderCatalog(decoders);

    private ILogger<DefaultClientCertificateResolver> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<PresentedClientCertificate?> ResolveCertificateAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var httpContext = openIdContext.Http;
        if (httpContext.Items.TryGetValue(ItemKey, out var resolved))
            return resolved as PresentedClientCertificate;

        var headerName = Options.CertificateHeaderName;
        var certificate = string.IsNullOrEmpty(headerName)
            ? await ReadConnectionCertificateAsync(httpContext, cancellationToken)
            : DecodeForwardedCertificate(openIdContext, headerName);

        httpContext.Items[ItemKey] = certificate ?? NoCertificate;
        return certificate;
    }

    /// <summary>
    /// Builds the format-to-decoder catalog, refusing two decoders that claim one format.
    /// </summary>
    internal static FrozenDictionary<string, IClientCertificateHeaderDecoder> CreateDecoderCatalog(
        IEnumerable<IClientCertificateHeaderDecoder> decoders
    )
    {
        var catalog = new Dictionary<string, IClientCertificateHeaderDecoder>(
            StringComparer.Ordinal
        );
        foreach (var decoder in decoders)
        {
            if (!catalog.TryAdd(decoder.Format, decoder))
            {
                throw new InvalidOperationException(
                    $"More than one {nameof(IClientCertificateHeaderDecoder)} is registered for the client certificate header format '{decoder.Format}'."
                );
            }
        }

        return catalog.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static async ValueTask<PresentedClientCertificate?> ReadConnectionCertificateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        // The connection owns its certificate, so it is not disposed with the request. Kestrel exposes only the
        // client's own certificate, never the rest of the chain it presented.
        var certificate =
            httpContext.Connection.ClientCertificate
            ?? await httpContext.Connection.GetClientCertificateAsync(cancellationToken);

        return certificate is null ? null : new PresentedClientCertificate(certificate);
    }

    private PresentedClientCertificate? DecodeForwardedCertificate(
        OpenIdContext openIdContext,
        string headerName
    )
    {
        var httpContext = openIdContext.Http;

        // A trusted proxy strips any value a client sent and adds the header only when the client presented a
        // certificate, so its absence means no certificate; the connection's own certificate is at most the proxy's.
        var headerValue = httpContext.Request.Headers[headerName].ToString();
        if (string.IsNullOrEmpty(headerValue))
            return null;

        var format = Options.Format;
        if (!Decoders.TryGetValue(format, out var decoder))
        {
            throw new InvalidOperationException(
                $"No {nameof(IClientCertificateHeaderDecoder)} is registered for the client certificate header format '{format}'."
            );
        }

        // A value the format cannot decode is refused rather than read as no certificate: it is a proxy that encodes
        // differently than configured, or a certificate the server cannot parse, and either must be visible.
        if (!decoder.TryDecode(headerValue, out var certificate))
        {
            Logger.ForwardedClientCertificateMalformed(headerName, format);
            throw openIdContext
                .ErrorFactory.InvalidRequest(
                    "The forwarded client certificate could not be decoded."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        httpContext.Response.RegisterForDispose(certificate.Certificate);
        foreach (var intermediate in certificate.Intermediates)
            httpContext.Response.RegisterForDispose(intermediate);

        return certificate;
    }
}

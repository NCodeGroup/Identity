#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;
using NCode.Disposables;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> that uses HTTP Basic Authentication.
/// </summary>
internal class ClientSecretBasicClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<ClientSecretBasicClientAuthenticationHandler> logger
)
    : CommonClientAuthenticationHandler(clientResolver, clientFactory, auditEventRecorder, logger),
        IClientAuthenticationHandler
{
    private static string UriDecode(Span<char> source)
    {
        source.Replace('+', ' ');
        return Uri.UnescapeDataString(source);
    }

    private static IDisposable UriDecodeSensitive(
        ReadOnlyMemory<char> encoded,
        out ReadOnlyMemory<char> decoded
    )
    {
        var source = encoded.Span;
        if (!source.ContainsAny('%', '+'))
        {
            decoded = encoded;
            return Disposable.Empty;
        }

        var lease = SecureMemoryPool<char>.Shared.Rent(encoded.Length);
        var buffer = lease.Memory;
        try
        {
            var destination = buffer.Span;
            source.Replace(destination, '+', ' ');

            var decodeResult = Uri.TryUnescapeDataString(
                destination,
                destination,
                out var unescapedLength
            );
            Debug.Assert(decodeResult);

            decoded = buffer[..unescapedLength];
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic;

    /// <inheritdoc />
    protected override ClientAuthenticationResult CreateInvalidClientResult(
        OpenIdContext openIdContext
    ) =>
        // The client authenticated via the HTTP Authorization header, so RFC 6749 §5.2 requires a 401 response
        // with a WWW-Authenticate challenge matching the scheme the client used.
        new(
            openIdContext
                .ErrorFactory.InvalidClient()
                .WithStatusCode(StatusCodes.Status401Unauthorized)
                .WithResponseHeader(
                    HeaderNames.WWWAuthenticate,
                    WwwAuthenticateChallenge.Basic(openIdContext.Tenant.Issuer)
                )
        );

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var httpContext = openIdContext.Http;

        var authorizationHeader = httpContext.Request.Headers.Authorization;
        if (authorizationHeader.Count != 1)
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        var authorizationValue = authorizationHeader[0].AsMemory();
        if (authorizationValue.IsEmpty)
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        const string prefix = "Basic ";
        if (!authorizationValue.Span.StartsWith(prefix, StringComparison.Ordinal))
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        var encodedCredentials = authorizationValue[prefix.Length..].Trim();
        if (encodedCredentials.IsEmpty)
            // The client attempted Basic authentication but supplied no credentials; this is a failed
            // authentication (invalid_client), which RFC 6749 §5.2 requires be answered with a 401 challenge.
            return CreateInvalidClientResult(openIdContext);

        return await AuthenticateCredentialsAsync(
            openIdContext,
            encodedCredentials,
            cancellationToken
        );
    }

    private async ValueTask<ClientAuthenticationResult> AuthenticateCredentialsAsync(
        OpenIdContext openIdContext,
        ReadOnlyMemory<char> encodedCredentials,
        CancellationToken cancellationToken
    )
    {
        // RFC 7617 encodes the user-id/password with standard Base64 (padded), not Base64Url. An upper bound of
        // length*3/4 is always large enough for the decoded bytes regardless of padding.
        var maxByteCount = encodedCredentials.Length * 3 / 4;
        using var base64Lease = BufferFactory.Rent(
            maxByteCount,
            isSensitive: true,
            out Span<byte> base64Buffer
        );

        // The credentials are untrusted input, so a decode failure must be handled explicitly (never asserted, which
        // is compiled out in Release); a malformed header is a failed authentication per RFC 6749 §5.2.
        if (
            !Convert.TryFromBase64Chars(
                encodedCredentials.Span,
                base64Buffer,
                out var base64BytesWritten
            )
        )
        {
            return CreateInvalidClientResult(openIdContext);
        }

        var base64Bytes = base64Buffer[..base64BytesWritten];

        var decodeCharCount = SecureEncoding.UTF8.GetCharCount(base64Bytes);
        using var decodeLease = SecureMemoryPool<char>.Shared.Rent(decodeCharCount);

        var decodeBuffer = decodeLease.Memory[..decodeCharCount];
        var decodeSpan = decodeBuffer.Span;

        var decodeResult = SecureEncoding.UTF8.TryGetChars(
            base64Bytes,
            decodeSpan,
            out var decodeCharsWritten
        );
        Debug.Assert(decodeResult && decodeCharsWritten == decodeCharCount);

        var indexOfColon = decodeSpan.IndexOf(':');

        var encodedClientId = indexOfColon == -1 ? decodeSpan : decodeSpan[..indexOfColon];
        var clientId = UriDecode(encodedClientId);

        if (indexOfColon == -1)
        {
            return await AuthenticateClientAsync(
                openIdContext,
                clientId,
                ReadOnlyMemory<char>.Empty,
                hasClientSecret: false,
                cancellationToken
            );
        }

        var encodedClientSecret = decodeBuffer[(indexOfColon + 1)..];

        using var secretLease = UriDecodeSensitive(encodedClientSecret, out var clientSecret);

        return await AuthenticateClientAsync(
            openIdContext,
            clientId,
            clientSecret,
            hasClientSecret: true,
            cancellationToken
        );
    }
}

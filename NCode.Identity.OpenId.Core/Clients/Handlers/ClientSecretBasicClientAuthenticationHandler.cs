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

using System.Buffers;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using NCode.CryptoMemory;
using NCode.Disposables;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Logic;
using Base64Url = NCode.Encoders.Base64Url;

namespace NCode.Identity.OpenId.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> that uses HTTP Basic Authentication.
/// </summary>
public class ClientSecretBasicClientAuthenticationHandler(
    IOpenIdErrorFactory errorFactory,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer
) : CommonClientAuthenticationHandler(errorFactory, storeManagerFactory, clientFactory, settingSerializer, secretSerializer),
    IClientAuthenticationHandler
{
    // TODO: move comment
    // RE: 400 vs 401
    // https://stackoverflow.com/questions/22586825/oauth-2-0-why-does-the-authorization-server-return-400-instead-of-401-when-the

    private IOpenIdError ErrorInvalidHeader { get; } = errorFactory
        .InvalidRequest("An invalid or malformed authorization header was provided.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private static string UriDecode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private static IDisposable UriDecode(
        bool isSensitive,
        ReadOnlyMemory<char> encoded,
        out ReadOnlyMemory<char> decoded)
    {
        var source = encoded.Span;
        if (!source.ContainsAny('%', '+'))
        {
            decoded = encoded;
            return Disposable.Empty;
        }

        var pool = isSensitive ?
            SecureMemoryPool<char>.Shared :
            MemoryPool<char>.Shared;

        var lease = pool.Rent(encoded.Length);
        var buffer = lease.Memory;
        try
        {
            var destination = buffer.Span;
            source.Replace(destination, '+', ' ');

            var decodeResult = Uri.TryUnescapeDataString(destination, destination, out var unescapedLength);
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
    public override string AuthenticationMethod => OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic;

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
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
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        return await AuthenticateCredentialsAsync(
            openIdContext,
            encodedCredentials,
            cancellationToken);
    }

    private async ValueTask<ClientAuthenticationResult> AuthenticateCredentialsAsync(
        OpenIdContext openIdContext,
        ReadOnlyMemory<char> encodedCredentials,
        CancellationToken cancellationToken)
    {
        var base64ByteCount = Base64Url.GetByteCountForDecode(encodedCredentials.Length);
        using var base64Lease = CryptoPool.Rent(base64ByteCount, isSensitive: true, out Span<byte> base64Bytes);

        var base64Result = Convert.TryFromBase64Chars(encodedCredentials.Span, base64Bytes, out var base64BytesWritten);
        Debug.Assert(base64Result && base64BytesWritten == base64ByteCount);

        var decodeCharCount = SecureEncoding.UTF8.GetCharCount(base64Bytes);
        using var decodeLease = SecureMemoryPool<char>.Shared.Rent(decodeCharCount);

        var decodeBuffer = decodeLease.Memory[..decodeCharCount];
        var decodeSpan = decodeBuffer.Span;

        var decodeResult = SecureEncoding.UTF8.TryGetChars(base64Bytes, decodeSpan, out var decodeCharsWritten);
        Debug.Assert(decodeResult && decodeCharsWritten == decodeCharCount);

        var indexOfColon = decodeSpan.IndexOf(':');

        var encodedClientId = indexOfColon == -1 ? decodeSpan : decodeSpan[..indexOfColon];
        var clientId = UriDecode(encodedClientId.ToString());

        if (indexOfColon == -1)
        {
            return await AuthenticateClientAsync(
                openIdContext,
                clientId,
                ReadOnlyMemory<char>.Empty,
                hasClientSecret: false,
                cancellationToken);
        }

        var encodedClientSecret = decodeBuffer[(indexOfColon + 1)..];

        using var secretLease = UriDecode(
            isSensitive: true,
            encodedClientSecret,
            out var clientSecret);

        return await AuthenticateClientAsync(
            openIdContext,
            clientId,
            clientSecret,
            hasClientSecret: true,
            cancellationToken);
    }
}

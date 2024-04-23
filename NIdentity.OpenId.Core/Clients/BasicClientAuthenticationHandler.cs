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
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using NCode.Jose.Buffers;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Clients;

internal class BasicClientAuthenticationHandler(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    IOpenIdErrorFactory errorFactory
) : IClientAuthenticationHandler
{
    // TODO: move comment
    // RE: 400 vs 401
    // https://stackoverflow.com/questions/22586825/oauth-2-0-why-does-the-authorization-server-return-400-instead-of-401-when-the

    private IOpenIdError ErrorInvalidHeader { get; } = errorFactory
        .InvalidRequest("An invalid or malformed authorization header was provided.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private IOpenIdError ErrorInvalidClient { get; } = errorFactory
        .InvalidClient()
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdClientFactory ClientFactory { get; } = clientFactory;

    /// <inheritdoc />
    public string AuthenticationMethod => "TODO"; // TODO

    private static string UriDecode(string value) =>
        Uri.UnescapeDataString(value.Replace("+", "%20"));

    /// <inheritdoc />
    public async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        var httpContext = openIdContext.Http;
        var authorizationHeader = httpContext.Request.Headers.Authorization;
        if (authorizationHeader.Count != 1)
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        var authorizationValue = authorizationHeader[0];
        if (string.IsNullOrEmpty(authorizationValue))
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        const string prefix = "Basic ";
        if (!authorizationValue.StartsWith(prefix, StringComparison.Ordinal))
            // not basic auth, let another handler try
            return ClientAuthenticationResult.Undefined;

        var encoded = authorizationValue[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(encoded))
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        // TODO: check for exceptions
        var credentials = SecureEncoding.Utf8.GetString(Convert.FromBase64String(encoded));
        if (string.IsNullOrEmpty(credentials))
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        var indexOfColon = credentials.IndexOf(':');
        var encodedClientId = indexOfColon == -1 ? credentials : credentials[..indexOfColon];
        var clientId = UriDecode(encodedClientId);
        if (string.IsNullOrEmpty(clientId))
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var clientStore = storeManager.GetStore<IClientStore>();

        var tenantId = openIdContext.Tenant.TenantId;
        var clientModel = await clientStore.TryGetByClientIdAsync(tenantId, clientId, cancellationToken);
        if (clientModel is null || clientModel.IsDisabled)
            return new ClientAuthenticationResult(ErrorInvalidClient);

        var publicClient = await ClientFactory.CreateAsync(openIdContext, clientModel, cancellationToken);
        if (indexOfColon == -1)
            return new ClientAuthenticationResult(publicClient);

        // TODO: use secure memory operations
        var encodedClientSecret = credentials[(indexOfColon + 1)..];
        var clientSecret = UriDecode(encodedClientSecret);
        var clientSecretBytes = SecureEncoding.Utf8.GetBytes(clientSecret);

        foreach (var secretKey in publicClient.SecretKeys.OfType<SymmetricSecretKey>())
        {
            using var _ = CryptoPool.Rent(
                secretKey.KeySizeBytes,
                isSensitive: true,
                out Memory<byte> encryptionKey);

            var exportResult = secretKey.TryExportPrivateKey(encryptionKey.Span, out var exportBytesWritten);
            Debug.Assert(exportResult && exportBytesWritten == secretKey.KeySizeBytes);

            if (!CryptographicOperations.FixedTimeEquals(encryptionKey.Span, clientSecretBytes))
                continue;

            var confidentialClient = await ClientFactory.CreateAsync(
                publicClient,
                AuthenticationMethod,
                secretKey,
                confirmation: null,
                cancellationToken);

            return new ClientAuthenticationResult(confidentialClient);
        }

        return new ClientAuthenticationResult(ErrorInvalidClient);
    }
}

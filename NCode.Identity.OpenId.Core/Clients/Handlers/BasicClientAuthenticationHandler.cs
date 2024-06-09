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

using Microsoft.AspNetCore.Http;
using NCode.CryptoMemory;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;

namespace NCode.Identity.OpenId.Clients.Handlers;

// TODO: registration

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> that uses HTTP Basic Authentication.
/// </summary>
public class BasicClientAuthenticationHandler(
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
        Uri.UnescapeDataString(value.Replace("+", "%20"));

    /// <inheritdoc />
    public override string AuthenticationMethod => OpenIdConstants.ClientAuthenticationMethods.Basic;

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
        var credentials = SecureEncoding.UTF8.GetString(Convert.FromBase64String(encoded));
        if (string.IsNullOrEmpty(credentials))
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        var indexOfColon = credentials.IndexOf(':');
        var encodedClientId = indexOfColon == -1 ? credentials : credentials[..indexOfColon];
        var clientId = UriDecode(encodedClientId);
        if (string.IsNullOrEmpty(clientId))
            return new ClientAuthenticationResult(ErrorInvalidHeader);

        var tenantId = openIdContext.Tenant.TenantId;
        var persistedClient = await TryGetPersistedClientAsync(tenantId, clientId, cancellationToken);
        if (persistedClient is null || persistedClient.IsDisabled)
            return new ClientAuthenticationResult(ErrorInvalidClient);

        var publicClient = await CreatePublicClientAsync(
            openIdContext,
            persistedClient,
            cancellationToken);

        if (indexOfColon == -1)
            return new ClientAuthenticationResult(publicClient);

        // TODO: use secure memory operations
        var encodedClientSecret = credentials[(indexOfColon + 1)..];
        var clientSecret = UriDecode(encodedClientSecret);
        var clientSecretBytes = SecureEncoding.UTF8.GetBytes(clientSecret);

        return await AuthenticateClientAsync(
            publicClient,
            clientSecretBytes,
            cancellationToken);
    }
}

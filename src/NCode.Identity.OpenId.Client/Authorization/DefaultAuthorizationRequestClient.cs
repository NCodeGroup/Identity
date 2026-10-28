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

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Tokens;

namespace NCode.Identity.OpenId.Client.Authorization;

/// <summary>
/// Provides a default implementation of <see cref="IAuthorizationRequestClient"/> that pushes the request when the
/// provider advertises a pushed authorization request endpoint (RFC 9126) and always uses PKCE <c>S256</c> (RFC 7636).
/// See ADR-0275.
/// </summary>
internal class DefaultAuthorizationRequestClient(
    IProviderMetadataResolver metadataResolver,
    ClientRequestSender sender
) : IAuthorizationRequestClient
{
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    private ClientRequestSender Sender { get; } = sender;

    /// <inheritdoc />
    public async ValueTask<AuthorizationRequestResult> CreateAuthorizationUriAsync(
        ClientRegistration registration,
        AuthorizationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var authorizationEndpoint = metadata.AuthorizationEndpoint;
        if (
            authorizationEndpoint is null
            || !ProviderUri.TryParseEndpoint(authorizationEndpoint, out _)
        )
        {
            throw new OpenIdProviderException(
                $"The provider '{registration.Authority}' has no acceptable authorization endpoint (https, or http on loopback)."
            );
        }

        if (
            metadata.CodeChallengeMethodsSupported is { Count: > 0 } methods
            && !methods.Contains("S256", StringComparer.Ordinal)
        )
        {
            throw new OpenIdProviderException(
                $"The provider '{registration.Authority}' does not support the PKCE 'S256' code challenge method."
            );
        }

        var parEndpoint = metadata.PushedAuthorizationRequestEndpoint;
        if (string.IsNullOrEmpty(parEndpoint))
        {
            if (
                metadata.RequirePushedAuthorizationRequests
                || registration.RequirePushedAuthorizationRequests
            )
            {
                throw new OpenIdProviderException(
                    $"Pushed authorization requests are required for the client '{registration.ClientId}', but the provider '{registration.Authority}' advertises no pushed_authorization_request_endpoint."
                );
            }

            return new AuthorizationRequestResult(
                new Uri(
                    AddQuery(authorizationEndpoint, BuildParameters(request, registration.ClientId))
                )
            );
        }

        var response = await Sender.PostFormAsync(
            registration,
            metadata,
            parEndpoint,
            BuildParameters(request, clientId: null),
            includeClientId: true,
            cancellationToken
        );

        if (
            response.StatusCode == StatusCodes.Status201Created
            && response.Json is { ValueKind: JsonValueKind.Object } json
        )
        {
            if (
                !json.TryGetProperty("request_uri", out var requestUriElement)
                || requestUriElement.ValueKind != JsonValueKind.String
                || requestUriElement.GetString() is not { Length: > 0 } requestUri
                || !json.TryGetProperty("expires_in", out var expiresInElement)
                || expiresInElement.ValueKind != JsonValueKind.Number
                || !expiresInElement.TryGetInt32(out var expiresIn)
                || expiresIn <= 0
            )
            {
                throw new OpenIdProviderException(
                    "The pushed authorization request endpoint answered without a request_uri or a positive expires_in."
                );
            }

            var authorizationUri = AddQuery(
                authorizationEndpoint,
                [
                    new KeyValuePair<string, string>("client_id", registration.ClientId),
                    new KeyValuePair<string, string>("request_uri", requestUri),
                ]
            );

            return new AuthorizationRequestResult(new Uri(authorizationUri), requestUri, expiresIn);
        }

        if (ProtocolErrorReader.TryRead(response, out var error))
            return new AuthorizationRequestResult(error);

        throw new OpenIdProviderException(
            $"The pushed authorization request endpoint answered HTTP {response.StatusCode} without an OAuth error."
        );
    }

    private static List<KeyValuePair<string, string>> BuildParameters(
        AuthorizationRequest request,
        string? clientId
    )
    {
        var parameters = new List<KeyValuePair<string, string>> { new("response_type", "code") };

        if (clientId is not null)
            parameters.Add(new KeyValuePair<string, string>("client_id", clientId));

        parameters.Add(new KeyValuePair<string, string>("redirect_uri", request.RedirectUri));
        parameters.Add(new KeyValuePair<string, string>("state", request.State));
        parameters.Add(
            new KeyValuePair<string, string>(
                "code_challenge",
                ComputeS256Challenge(request.CodeVerifier)
            )
        );
        parameters.Add(new KeyValuePair<string, string>("code_challenge_method", "S256"));

        if (!string.IsNullOrEmpty(request.Nonce))
            parameters.Add(new KeyValuePair<string, string>("nonce", request.Nonce));

        if (!string.IsNullOrEmpty(request.Scope))
            parameters.Add(new KeyValuePair<string, string>("scope", request.Scope));

        if (!string.IsNullOrEmpty(request.ResponseMode))
            parameters.Add(new KeyValuePair<string, string>("response_mode", request.ResponseMode));

        foreach (var resource in request.Resources)
            parameters.Add(new KeyValuePair<string, string>("resource", resource));

        if (request.AdditionalParameters is { } additionalParameters)
        {
            foreach (var (name, value) in additionalParameters)
                parameters.Add(new KeyValuePair<string, string>(name, value));
        }

        return parameters;
    }

    private static string ComputeS256Challenge(string verifier) =>
        Base64Url.Encode(SHA256.HashData(SecureEncoding.ASCII.GetBytes(verifier)));

    private static string AddQuery(
        string endpoint,
        List<KeyValuePair<string, string>> parameters
    ) =>
        QueryHelpers.AddQueryString(
            endpoint,
            parameters.Select(static p => new KeyValuePair<string, string?>(p.Key, p.Value))
        );
}

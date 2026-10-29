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
using System.Text;
using System.Text.Json;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Json;

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
            && !methods.Contains(OpenIdProtocol.CodeChallengeMethods.S256, StringComparer.Ordinal)
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
            response.StatusCode == ProviderStatusCodes.Status201Created
            && response.Json is { ValueKind: JsonValueKind.Object } json
        )
        {
            if (
                !json.TryGetProperty(
                    OpenIdProtocol.Parameters.RequestUri,
                    out var requestUriElement
                )
                || requestUriElement.ValueKind != JsonValueKind.String
                || requestUriElement.GetString() is not { Length: > 0 } requestUri
                || !json.TryGetPropertyWholeInt32(
                    OpenIdProtocol.Parameters.ExpiresIn,
                    out var expiresIn
                )
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
                    new KeyValuePair<string, string>(
                        OpenIdProtocol.Parameters.ClientId,
                        registration.ClientId
                    ),
                    new KeyValuePair<string, string>(
                        OpenIdProtocol.Parameters.RequestUri,
                        requestUri
                    ),
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
        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OpenIdProtocol.Parameters.ResponseType, OpenIdProtocol.ResponseTypes.Code),
        };

        if (clientId is not null)
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.ClientId, clientId)
            );

        parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.RedirectUri,
                request.RedirectUri
            )
        );
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.State, request.State)
        );
        parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.CodeChallenge,
                ComputeS256Challenge(request.CodeVerifier)
            )
        );
        parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.CodeChallengeMethod,
                OpenIdProtocol.CodeChallengeMethods.S256
            )
        );

        if (!string.IsNullOrEmpty(request.Nonce))
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.Nonce, request.Nonce)
            );

        if (!string.IsNullOrEmpty(request.Scope))
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.Scope, request.Scope)
            );

        if (!string.IsNullOrEmpty(request.ResponseMode))
            parameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.ResponseMode,
                    request.ResponseMode
                )
            );

        foreach (var resource in request.Resources)
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.Resource, resource)
            );

        if (request.AdditionalParameters is { } additionalParameters)
        {
            foreach (var (name, value) in additionalParameters)
                parameters.Add(new KeyValuePair<string, string>(name, value));
        }

        return parameters;
    }

    private static string ComputeS256Challenge(string verifier) =>
        Base64Url.Encode(SHA256.HashData(SecureEncoding.ASCII.GetBytes(verifier)));

    private static string AddQuery(string endpoint, List<KeyValuePair<string, string>> parameters)
    {
        var builder = new StringBuilder(endpoint);

        // ProviderUri.TryParseEndpoint rejects a fragment, so the query is appended at the end.
        var separator = endpoint.Contains('?') ? '&' : '?';
        foreach (var (name, value) in parameters)
        {
            builder
                .Append(separator)
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return builder.ToString();
    }
}

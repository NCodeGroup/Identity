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

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.OpenApi;

/// <summary>
/// Describes the token endpoint's <c>application/x-www-form-urlencoded</c> request body and attaches a named example per
/// legitimate grant flow (<c>client_credentials</c>, <c>authorization_code</c>, <c>refresh_token</c>), so a renderer
/// such as Scalar shows the form fields and lets the caller pick a pre-filled example. The .NET OpenAPI generator does
/// not reflect the endpoint's form metadata into the document, so the body is set here explicitly.
/// </summary>
internal sealed class TokenEndpointExamplesDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (TryGetTokenOperations(document, out var operations))
        {
            foreach (var operation in operations)
            {
                operation.RequestBody = BuildRequestBody();
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Enumerates the operations mapped to the token endpoint path, if any.
    /// </summary>
    internal static bool TryGetTokenOperations(
        OpenApiDocument document,
        out IEnumerable<OpenApiOperation> operations
    )
    {
        if (
            document.Paths is not null
            && document.Paths.TryGetValue(OpenIdConstants.EndpointPaths.Token, out var pathItem)
            && pathItem.Operations is { Count: > 0 } map
        )
        {
            operations = map.Values;
            return true;
        }

        operations = [];
        return false;
    }

    private static OpenApiRequestBody BuildRequestBody()
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            [OpenIdProtocol.Parameters.GrantType] = StringSchema(
                "The grant type being requested, such as authorization_code, client_credentials, or refresh_token."
            ),
            [OpenIdProtocol.Parameters.ClientId] = StringSchema(
                "The identifier issued to the client during registration."
            ),
            [OpenIdProtocol.Parameters.ClientSecret] = StringSchema(
                "The client's secret, for clients that authenticate with a shared secret."
            ),
            [OpenIdProtocol.Parameters.Scope] = StringSchema(
                "A space-delimited list of requested scopes."
            ),
            [OpenIdProtocol.Parameters.AuthorizationCode] = StringSchema(
                "The authorization code returned by the authorization endpoint (authorization_code grant)."
            ),
            [OpenIdProtocol.Parameters.RedirectUri] = StringSchema(
                "The client's redirect URI; must match the value used in the authorization request."
            ),
            [OpenIdProtocol.Parameters.CodeVerifier] = StringSchema(
                "The PKCE code verifier (RFC 7636) matching the code_challenge sent to the authorization endpoint."
            ),
            [OpenIdProtocol.Parameters.RefreshToken] = StringSchema(
                "The refresh token previously issued to the client (refresh_token grant)."
            ),
        };

        var mediaType = new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties },
            Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal)
            {
                [OpenIdProtocol.GrantTypes.ClientCredentials] = ClientCredentialsExample(),
                [OpenIdProtocol.GrantTypes.AuthorizationCode] = AuthorizationCodeExample(),
                [OpenIdProtocol.GrantTypes.RefreshToken] = RefreshTokenExample(),
            },
        };

        return new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [OpenIdProtocol.MediaTypes.FormUrlEncoded] = mediaType,
            },
        };
    }

    private static OpenApiSchema StringSchema(string description) =>
        new() { Type = JsonSchemaType.String, Description = description };

    private static OpenApiExample ClientCredentialsExample() =>
        new()
        {
            Summary = "Client credentials",
            Description =
                "Exchange a confidential client's credentials for an access token (machine-to-machine).",
            Value = new JsonObject
            {
                [OpenIdProtocol.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .ClientCredentials,
                [OpenIdProtocol.Parameters.ClientId] = "your-client-id",
                [OpenIdProtocol.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdProtocol.Parameters.Scope] = "read:clients",
            },
        };

    private static OpenApiExample AuthorizationCodeExample() =>
        new()
        {
            Summary = "Authorization code",
            Description = "Exchange an authorization code (with PKCE) for tokens.",
            Value = new JsonObject
            {
                [OpenIdProtocol.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .AuthorizationCode,
                [OpenIdProtocol.Parameters.AuthorizationCode] = "the-authorization-code",
                [OpenIdProtocol.Parameters.RedirectUri] = "https://your-app.example/callback",
                [OpenIdProtocol.Parameters.ClientId] = "your-client-id",
                [OpenIdProtocol.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdProtocol.Parameters.CodeVerifier] = "the-pkce-code-verifier",
            },
        };

    private static OpenApiExample RefreshTokenExample() =>
        new()
        {
            Summary = "Refresh token",
            Description = "Exchange a refresh token for a new access token.",
            Value = new JsonObject
            {
                [OpenIdProtocol.Parameters.GrantType] = OpenIdProtocol.GrantTypes.RefreshToken,
                [OpenIdProtocol.Parameters.RefreshToken] = "the-refresh-token",
                [OpenIdProtocol.Parameters.ClientId] = "your-client-id",
                [OpenIdProtocol.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdProtocol.Parameters.Scope] = "read:clients",
            },
        };
}

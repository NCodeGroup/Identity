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
            [OpenIdConstants.Parameters.GrantType] = StringSchema(),
            [OpenIdConstants.Parameters.ClientId] = StringSchema(),
            [OpenIdConstants.Parameters.ClientSecret] = StringSchema(),
            [OpenIdConstants.Parameters.Scope] = StringSchema(),
            [OpenIdConstants.Parameters.AuthorizationCode] = StringSchema(),
            [OpenIdConstants.Parameters.RedirectUri] = StringSchema(),
            [OpenIdConstants.Parameters.CodeVerifier] = StringSchema(),
            [OpenIdConstants.Parameters.RefreshToken] = StringSchema(),
        };

        var mediaType = new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties },
            Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal)
            {
                [OpenIdConstants.GrantTypes.ClientCredentials] = ClientCredentialsExample(),
                [OpenIdConstants.GrantTypes.AuthorizationCode] = AuthorizationCodeExample(),
                [OpenIdConstants.GrantTypes.RefreshToken] = RefreshTokenExample(),
            },
        };

        return new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [OpenIdConstants.ContentType] = mediaType,
            },
        };
    }

    private static OpenApiSchema StringSchema() => new() { Type = JsonSchemaType.String };

    private static OpenApiExample ClientCredentialsExample() =>
        new()
        {
            Summary = "Client credentials",
            Description =
                "Exchange a confidential client's credentials for an access token (machine-to-machine).",
            Value = new JsonObject
            {
                [OpenIdConstants.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .ClientCredentials,
                [OpenIdConstants.Parameters.ClientId] = "your-client-id",
                [OpenIdConstants.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdConstants.Parameters.Scope] = "read:clients",
            },
        };

    private static OpenApiExample AuthorizationCodeExample() =>
        new()
        {
            Summary = "Authorization code",
            Description = "Exchange an authorization code (with PKCE) for tokens.",
            Value = new JsonObject
            {
                [OpenIdConstants.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .AuthorizationCode,
                [OpenIdConstants.Parameters.AuthorizationCode] = "the-authorization-code",
                [OpenIdConstants.Parameters.RedirectUri] = "https://your-app.example/callback",
                [OpenIdConstants.Parameters.ClientId] = "your-client-id",
                [OpenIdConstants.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdConstants.Parameters.CodeVerifier] = "the-pkce-code-verifier",
            },
        };

    private static OpenApiExample RefreshTokenExample() =>
        new()
        {
            Summary = "Refresh token",
            Description = "Exchange a refresh token for a new access token.",
            Value = new JsonObject
            {
                [OpenIdConstants.Parameters.GrantType] = OpenIdConstants.GrantTypes.RefreshToken,
                [OpenIdConstants.Parameters.RefreshToken] = "the-refresh-token",
                [OpenIdConstants.Parameters.ClientId] = "your-client-id",
                [OpenIdConstants.Parameters.ClientSecret] = "your-client-secret",
                [OpenIdConstants.Parameters.Scope] = "read:clients",
            },
        };
}

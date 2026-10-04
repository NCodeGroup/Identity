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
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NCode.Identity.OpenId;
using NCode.Identity.Server;

namespace NCode.Identity.OpenId.Playground;

/// <summary>
/// A development-only OpenAPI transformer that appends a pre-filled <c>client_credentials</c> example for the configured
/// bootstrap administrator to the token endpoint, so an operator can obtain the GlobalAdmin token from Scalar in one
/// click. It is inert unless the bootstrap administrator is configured, and runs after the generic token examples are
/// added by the composition root.
/// </summary>
internal sealed class BootstrapTokenExampleDocumentTransformer(
    IOptions<BootstrapAdminOptions> optionsAccessor
) : IOpenApiDocumentTransformer
{
    private const string ExampleKey = "bootstrap_admin";

    // The scope only binds the urn:ncode:management audience; the GlobalAdmin role (not the scope) grants authority.
    private const string BootstrapScope = "read:clients";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var options = optionsAccessor.Value;
        var clientId = options.ClientId;
        var clientSecret = options.ClientSecret;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return Task.CompletedTask;
        }

        if (
            document.Paths is null
            || !document.Paths.TryGetValue(OpenIdConstants.EndpointPaths.Token, out var pathItem)
            || pathItem.Operations is not { Count: > 0 } operations
        )
        {
            return Task.CompletedTask;
        }

        var example = new OpenApiExample
        {
            Summary = "Client credentials (bootstrap admin)",
            Description =
                "Exchange the configured bootstrap administrator credentials for a GlobalAdmin access token.",
            Value = new JsonObject
            {
                [OpenIdConstants.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .ClientCredentials,
                [OpenIdConstants.Parameters.ClientId] = clientId,
                [OpenIdConstants.Parameters.ClientSecret] = clientSecret,
                [OpenIdConstants.Parameters.Scope] = BootstrapScope,
            },
        };

        foreach (var operation in operations.Values)
        {
            if (
                operation.RequestBody?.Content is { } content
                && content.TryGetValue(OpenIdConstants.ContentType, out var mediaType)
                && mediaType.Examples is { } examples
            )
            {
                examples[ExampleKey] = example;
            }
        }

        return Task.CompletedTask;
    }
}

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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NCode.Identity.OpenId;
using NCode.Identity.Server.DevelopmentEnvironment;

namespace NCode.Identity.Server.OpenApi;

/// <summary>
/// An OpenAPI transformer that, <strong>in the development environment only</strong>, appends a pre-filled
/// <c>client_credentials</c> example for the configured bootstrap administrator to the token endpoint, so an operator
/// can obtain the GlobalAdmin token from a renderer such as Scalar in one click. It also marks the four
/// <c>client_credentials</c> form fields required with default values so the renderer shows them pre-checked and
/// populated (otherwise every optional field starts unchecked and is omitted from the request). It is inert outside
/// development and unless the bootstrap administrator is configured, so the secret never appears in a non-development
/// document, and runs after the generic token examples are added by the composition root.
/// </summary>
internal sealed class BootstrapTokenExampleDocumentTransformer(
    IHostEnvironment hostEnvironment,
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
        // The pre-filled example embeds the (decoded) bootstrap secret, so it is a development-only convenience.
        if (!hostEnvironment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        var options = optionsAccessor.Value;
        var clientId = options.ClientId;
        var clientSecret = options.GetPresentableClientSecretOrDefault();
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

        (string Name, string Value)[] fields =
        [
            (OpenIdConstants.Parameters.GrantType, OpenIdConstants.GrantTypes.ClientCredentials),
            (OpenIdConstants.Parameters.ClientId, clientId),
            (OpenIdConstants.Parameters.ClientSecret, clientSecret),
            (OpenIdConstants.Parameters.Scope, BootstrapScope),
        ];

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
                operation.RequestBody?.Content is not { } content
                || !content.TryGetValue(OpenIdConstants.ContentType, out var mediaType)
            )
            {
                continue;
            }

            if (mediaType.Examples is { } examples)
            {
                examples[ExampleKey] = example;
            }

            PrefillBootstrapFields(mediaType.Schema, fields);
        }

        return Task.CompletedTask;
    }

    private static void PrefillBootstrapFields(
        IOpenApiSchema? schema,
        IReadOnlyList<(string Name, string Value)> fields
    )
    {
        if (schema is not OpenApiSchema formSchema || formSchema.Properties is not { } properties)
        {
            return;
        }

        // Required fields render pre-checked in a renderer such as Scalar; the default supplies the pre-filled value.
        formSchema.Required ??= new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, value) in fields)
        {
            if (
                properties.TryGetValue(name, out var property)
                && property is OpenApiSchema fieldSchema
            )
            {
                fieldSchema.Default = JsonValue.Create(value);
            }

            formSchema.Required.Add(name);
        }
    }
}

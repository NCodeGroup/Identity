// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.DevelopmentEnvironment.OpenApi;

/// <summary>
/// An OpenAPI transformer that, <strong>when <c>LocalDevelopment:Enabled</c> is true only</strong>, appends a
/// pre-filled <c>client_credentials</c> example for the configured bootstrap administrator to the token endpoint, so an
/// operator can obtain the GlobalAdmin token from a renderer such as Scalar in one click. It also marks the four
/// <c>client_credentials</c> form fields required with default values so the renderer shows them pre-checked and
/// populated (otherwise every optional field starts unchecked and is omitted from the request). It is inert unless
/// the switch is on and the bootstrap administrator is configured, so the secret never appears in a deployment's
/// document, and runs after the generic token examples are added by the composition root.
/// </summary>
internal sealed class BootstrapTokenExampleDocumentTransformer(
    LocalDevelopmentMode localDevelopment,
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
        // The pre-filled example embeds the (decoded) bootstrap secret, so it is a local-development convenience.
        if (!localDevelopment.Enabled)
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
            (OpenIdProtocol.Parameters.GrantType, OpenIdProtocol.GrantTypes.ClientCredentials),
            (OpenIdProtocol.Parameters.ClientId, clientId),
            (OpenIdProtocol.Parameters.ClientSecret, clientSecret),
            (OpenIdProtocol.Parameters.Scope, BootstrapScope),
        ];

        var example = new OpenApiExample
        {
            Summary = "Client credentials (bootstrap admin)",
            Description =
                "Exchange the configured bootstrap administrator credentials for a GlobalAdmin access token.",
            Value = new JsonObject
            {
                [OpenIdProtocol.Parameters.GrantType] = OpenIdProtocol.GrantTypes.ClientCredentials,
                [OpenIdProtocol.Parameters.ClientId] = clientId,
                [OpenIdProtocol.Parameters.ClientSecret] = clientSecret,
                [OpenIdProtocol.Parameters.Scope] = BootstrapScope,
            },
        };

        foreach (var operation in operations.Values)
        {
            if (
                operation.RequestBody?.Content is not { } content
                || !content.TryGetValue(OpenIdProtocol.MediaTypes.FormUrlEncoded, out var mediaType)
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

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.OpenId;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.DevelopmentEnvironment.OpenApi;
using Xunit;

namespace NCode.Identity.Server.Tests.DevelopmentEnvironment.OpenApi;

public class BootstrapTokenExampleDocumentTransformerTests
{
    private const string ExampleKey = "bootstrap_admin";

    // The encoded form of the secret 'secret'.
    private const string EncodedSecret = "c2VjcmV0";

    #region Helpers

    private static BootstrapTokenExampleDocumentTransformer CreateTransformer(
        bool localDevelopmentEnabled,
        BootstrapAdminOptions bootstrapOptions
    ) =>
        new(
            Options.Create(new LocalDevelopmentOptions { Enabled = localDevelopmentEnabled }),
            Options.Create(bootstrapOptions)
        );

    private static BootstrapAdminOptions CreateConfiguredBootstrapOptions() =>
        new()
        {
            ClientId = "bootstrap",
            ClientSecret = EncodedSecret,
            TenantId = "tenant",
        };

    private static OpenApiMediaType CreateTokenMediaType() =>
        new()
        {
            Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal),
            Schema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    [OpenIdProtocol.Parameters.ClientId] = new OpenApiSchema(),
                    [OpenIdProtocol.Parameters.ClientSecret] = new OpenApiSchema(),
                },
            },
        };

    private static OpenApiDocument CreateDocument(OpenApiMediaType mediaType) =>
        new()
        {
            Paths = new OpenApiPaths
            {
                [OpenIdConstants.EndpointPaths.Token] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new()
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>(
                                    StringComparer.Ordinal
                                )
                                {
                                    [OpenIdProtocol.MediaTypes.FormUrlEncoded] = mediaType,
                                },
                            },
                        },
                    },
                },
            },
        };

    private static async ValueTask TransformAsync(
        BootstrapTokenExampleDocumentTransformer transformer,
        OpenApiDocument document,
        CancellationToken cancellationToken
    )
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new OpenApiDocumentTransformerContext
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = serviceProvider,
        };

        await transformer.TransformAsync(document, context, cancellationToken);
    }

    #endregion

    #region TransformAsync Tests

    [Fact]
    public async Task TransformAsync_WhenLocalDevelopmentIsEnabled_AddsTheExampleAndPrefillsTheFields()
    {
        var mediaType = CreateTokenMediaType();
        var transformer = CreateTransformer(true, CreateConfiguredBootstrapOptions());

        await TransformAsync(transformer, CreateDocument(mediaType), CancellationToken.None);

        Assert.NotNull(mediaType.Examples);
        Assert.True(mediaType.Examples.ContainsKey(ExampleKey));
        var schema = Assert.IsType<OpenApiSchema>(mediaType.Schema);
        Assert.NotNull(schema.Required);
        Assert.Contains(OpenIdProtocol.Parameters.ClientId, schema.Required);
        Assert.NotNull(schema.Properties);
        var clientIdDefault = Assert.IsAssignableFrom<JsonValue>(
            schema.Properties[OpenIdProtocol.Parameters.ClientId].Default
        );
        Assert.Equal("bootstrap", clientIdDefault.GetValue<string>());
        var clientSecretDefault = Assert.IsAssignableFrom<JsonValue>(
            schema.Properties[OpenIdProtocol.Parameters.ClientSecret].Default
        );
        Assert.Equal("secret", clientSecretDefault.GetValue<string>());
    }

    [Fact]
    public async Task TransformAsync_WhenLocalDevelopmentIsNotEnabled_AddsNothing()
    {
        var mediaType = CreateTokenMediaType();
        var transformer = CreateTransformer(false, CreateConfiguredBootstrapOptions());

        await TransformAsync(transformer, CreateDocument(mediaType), CancellationToken.None);

        Assert.NotNull(mediaType.Examples);
        Assert.Empty(mediaType.Examples);
        var schema = Assert.IsType<OpenApiSchema>(mediaType.Schema);
        Assert.True(schema.Required is null || schema.Required.Count == 0);
    }

    [Fact]
    public async Task TransformAsync_WhenLocalDevelopmentIsEnabledWithoutABootstrapAdministrator_AddsNothing()
    {
        var mediaType = CreateTokenMediaType();
        var transformer = CreateTransformer(true, new BootstrapAdminOptions());

        await TransformAsync(transformer, CreateDocument(mediaType), CancellationToken.None);

        Assert.NotNull(mediaType.Examples);
        Assert.Empty(mediaType.Examples);
    }

    #endregion
}

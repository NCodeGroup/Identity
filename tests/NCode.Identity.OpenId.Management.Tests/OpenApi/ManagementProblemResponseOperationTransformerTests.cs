// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace NCode.Identity.OpenId.Management.OpenApi;

public sealed class ManagementProblemResponseOperationTransformerTests
{
    #region TransformAsync Tests

    [Fact]
    public async Task TransformAsync_WhenA401HasOnlyItsReasonPhrase_FillsTheDefaultDescription()
    {
        var operation = CreateOperation(("401", "Unauthorized"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.UnauthorizedDescription,
            DescriptionOf(operation, "401")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA403HasItsOwnDescription_KeepsIt()
    {
        var operation = CreateOperation(("403", "The caller is not the owner."));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal("The caller is not the owner.", DescriptionOf(operation, "403"));
    }

    [Fact]
    public async Task TransformAsync_WhenA409HasNoDescription_FillsTheStoredSettingsSentence()
    {
        var operation = CreateOperation(("409", "Conflict"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.StoredSettingsConflictDescription,
            DescriptionOf(operation, "409")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA409HasItsOwnDescription_AppendsTheStoredSettingsSentence()
    {
        var operation = CreateOperation(("409", "The tenant already exists."));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            "The tenant already exists. "
                + ManagementProblemResponseOperationTransformer.StoredSettingsConflictDescription,
            DescriptionOf(operation, "409")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA503HasNoDescription_FillsTheRootKeySentence()
    {
        var operation = CreateOperation(("503", "Service Unavailable"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.RootKeyUnavailableDescription,
            DescriptionOf(operation, "503")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA400BindsABody_AppendsTheBindingSentence()
    {
        var operation = CreateOperation(("400", "The settings are invalid."));
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(
            new ApiParameterDescription { Source = BindingSource.Body, Type = typeof(object) }
        );

        await TransformAsync(operation, description, CancellationToken.None);

        Assert.Equal(
            "The settings are invalid. "
                + ManagementProblemResponseOperationTransformer.BindingFailureDescription,
            DescriptionOf(operation, "400")
        );
    }

    [Theory]
    [InlineData("Query", typeof(int?), false)]
    [InlineData("Header", typeof(int), true)]
    [InlineData("Path", typeof(Guid), true)]
    [InlineData("Query", typeof(string), true)]
    [InlineData("Header", typeof(string), true)]
    public async Task TransformAsync_WhenA400BindsAParameterThatCanFail_AppendsTheBindingSentence(
        string source,
        Type type,
        bool isRequired
    )
    {
        var operation = CreateOperation(("400", "The settings are invalid."));
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(
            new ApiParameterDescription
            {
                Source = BindingSourceOf(source),
                Type = type,
                IsRequired = isRequired,
            }
        );

        await TransformAsync(operation, description, CancellationToken.None);

        Assert.Equal(
            "The settings are invalid. "
                + ManagementProblemResponseOperationTransformer.BindingFailureDescription,
            DescriptionOf(operation, "400")
        );
    }

    [Theory]
    [InlineData("Query", typeof(string), false)]
    [InlineData("Header", typeof(string), false)]
    [InlineData("Path", typeof(string), true)]
    [InlineData("Form", typeof(int), true)]
    public async Task TransformAsync_WhenA400BindsAParameterThatCannotFail_KeepsItsOwnDescription(
        string source,
        Type type,
        bool isRequired
    )
    {
        var operation = CreateOperation(("400", "The settings are invalid."));
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(
            new ApiParameterDescription
            {
                Source = BindingSourceOf(source),
                Type = type,
                IsRequired = isRequired,
            }
        );

        await TransformAsync(operation, description, CancellationToken.None);

        Assert.Equal("The settings are invalid.", DescriptionOf(operation, "400"));
    }

    [Fact]
    public async Task TransformAsync_WhenA403HasOnlyItsReasonPhrase_FillsTheDefaultDescription()
    {
        var operation = CreateOperation(("403", "Forbidden"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.ForbiddenDescription,
            DescriptionOf(operation, "403")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA412HasOnlyItsReasonPhrase_FillsTheDefaultDescription()
    {
        var operation = CreateOperation(("412", "Precondition Failed"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.PreconditionFailedDescription,
            DescriptionOf(operation, "412")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA415HasOnlyItsReasonPhrase_FillsTheDefaultDescription()
    {
        var operation = CreateOperation(("415", "Unsupported Media Type"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.UnsupportedMediaTypeDescription,
            DescriptionOf(operation, "415")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenAResponseIsASuccess_LeavesItUntouched()
    {
        var operation = CreateOperation(("200", "OK"));

        await TransformAsync(operation, new ApiDescription(), CancellationToken.None);

        Assert.Equal("OK", DescriptionOf(operation, "200"));
    }

    #endregion

    private static OpenApiOperation CreateOperation(
        params (string Code, string Description)[] responses
    )
    {
        var operation = new OpenApiOperation { Responses = new OpenApiResponses() };
        foreach (var (code, description) in responses)
        {
            operation.Responses[code] = new OpenApiResponse { Description = description };
        }

        return operation;
    }

    private static BindingSource BindingSourceOf(string source) =>
        source switch
        {
            "Query" => BindingSource.Query,
            "Header" => BindingSource.Header,
            "Path" => BindingSource.Path,
            "Form" => BindingSource.Form,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    private static string? DescriptionOf(OpenApiOperation operation, string code) =>
        operation.Responses?[code].Description;

    private static async ValueTask TransformAsync(
        OpenApiOperation operation,
        ApiDescription description,
        CancellationToken cancellationToken
    )
    {
        var context = new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        await new ManagementProblemResponseOperationTransformer().TransformAsync(
            operation,
            context,
            cancellationToken
        );
    }
}

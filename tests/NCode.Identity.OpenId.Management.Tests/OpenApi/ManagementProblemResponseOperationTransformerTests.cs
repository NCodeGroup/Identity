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

        await TransformAsync(operation, new ApiDescription());

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.UnauthorizedDescription,
            DescriptionOf(operation, "401")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA403HasItsOwnDescription_KeepsIt()
    {
        var operation = CreateOperation(("403", "The caller is not the owner."));

        await TransformAsync(operation, new ApiDescription());

        Assert.Equal("The caller is not the owner.", DescriptionOf(operation, "403"));
    }

    [Fact]
    public async Task TransformAsync_WhenA409HasNoDescription_FillsTheStoredSettingsSentence()
    {
        var operation = CreateOperation(("409", "Conflict"));

        await TransformAsync(operation, new ApiDescription());

        Assert.Equal(
            ManagementProblemResponseOperationTransformer.StoredSettingsConflictDescription,
            DescriptionOf(operation, "409")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA409HasItsOwnDescription_AppendsTheStoredSettingsSentence()
    {
        var operation = CreateOperation(("409", "The tenant already exists."));

        await TransformAsync(operation, new ApiDescription());

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

        await TransformAsync(operation, new ApiDescription());

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

        await TransformAsync(operation, description);

        Assert.Equal(
            "The settings are invalid. "
                + ManagementProblemResponseOperationTransformer.BindingFailureDescription,
            DescriptionOf(operation, "400")
        );
    }

    [Fact]
    public async Task TransformAsync_WhenA400BindsNothingThatCanFail_KeepsItsOwnDescription()
    {
        var operation = CreateOperation(("400", "The settings are invalid."));
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(
            new ApiParameterDescription
            {
                Source = BindingSource.Path,
                Type = typeof(string),
                IsRequired = true,
            }
        );

        await TransformAsync(operation, description);

        Assert.Equal("The settings are invalid.", DescriptionOf(operation, "400"));
    }

    [Fact]
    public async Task TransformAsync_WhenAResponseIsASuccess_LeavesItUntouched()
    {
        var operation = CreateOperation(("200", "OK"));

        await TransformAsync(operation, new ApiDescription());

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

    private static string? DescriptionOf(OpenApiOperation operation, string code) =>
        operation.Responses?[code].Description;

    private static Task TransformAsync(OpenApiOperation operation, ApiDescription description)
    {
        var context = new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        return new ManagementProblemResponseOperationTransformer().TransformAsync(
            operation,
            context,
            CancellationToken.None
        );
    }
}

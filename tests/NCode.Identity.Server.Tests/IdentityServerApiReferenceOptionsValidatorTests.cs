// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class IdentityServerApiReferenceOptionsValidatorTests
{
    private static IdentityServerApiReferenceOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenNothingIsEnabled_Succeeds()
    {
        var result = Validator.Validate(name: null, new IdentityServerApiReferenceOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOnlyTheDocumentIsEnabled_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new IdentityServerApiReferenceOptions { DocumentEnabled = true }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenBothAreEnabled_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new IdentityServerApiReferenceOptions { DocumentEnabled = true, PageEnabled = true }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenThePageIsEnabledWithoutTheDocument_Fails()
    {
        // The page renders the document this host serves, so it cannot work without it.
        var result = Validator.Validate(
            name: null,
            new IdentityServerApiReferenceOptions { PageEnabled = true }
        );

        Assert.True(result.Failed);
        Assert.Equal(
            "ApiReference:PageEnabled requires ApiReference:DocumentEnabled: the API reference page renders the OpenAPI document this host serves; PageEnabled is true and DocumentEnabled is false.",
            result.FailureMessage
        );
    }

    #endregion
}

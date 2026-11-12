// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HttpLogging;
using NCode.Identity.AspNetCore.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.Options;

public sealed class GuardedHttpLoggingOptionsValidatorTests
{
    private static GuardedHttpLoggingOptionsValidator Validator { get; } = new();

    private static HttpLoggingOptions CreateOptions(HttpLoggingFields loggingFields)
    {
        var options = new HttpLoggingOptions { LoggingFields = loggingFields };
        options.ResponseHeaders.Remove("Location");
        return options;
    }

    private static HttpLoggingOptions CreatePermittedOptions() =>
        CreateOptions(GuardedHttpLoggingDefaults.PermittedFields);

    #region Validate Tests

    [Fact]
    public void Validate_WhenPermittedFieldsAndNoCredentialHeader_Succeeds()
    {
        var result = Validator.Validate(name: null, CreatePermittedOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(HttpLoggingFields.RequestBody)]
    [InlineData(HttpLoggingFields.ResponseBody)]
    [InlineData(HttpLoggingFields.RequestQuery)]
    [InlineData(HttpLoggingFields.RequestTrailers)]
    [InlineData(HttpLoggingFields.ResponseTrailers)]
    public void Validate_WhenAFieldOutsideThePermittedSetIsEnabled_FailsNamingIt(
        HttpLoggingFields field
    )
    {
        var result = Validator.Validate(
            name: null,
            CreateOptions(GuardedHttpLoggingDefaults.PermittedFields | field)
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains($"enables '{field}'", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("authorization")]
    [InlineData("Proxy-Authorization")]
    [InlineData("Cookie")]
    [InlineData("DPoP")]
    public void Validate_WhenARequestCredentialHeaderIsAllowListed_FailsNamingIt(string header)
    {
        var options = CreatePermittedOptions();
        options.RequestHeaders.Add(header);

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("RequestHeaders", failure, StringComparison.Ordinal);
        Assert.Contains($"'{header}'", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Set-Cookie")]
    [InlineData("Location")]
    public void Validate_WhenAResponseCredentialHeaderIsAllowListed_FailsNamingIt(string header)
    {
        var options = CreatePermittedOptions();
        options.ResponseHeaders.Add(header);

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("ResponseHeaders", failure, StringComparison.Ordinal);
        Assert.Contains($"'{header}'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenSeveralProblems_ReportsEveryOne()
    {
        var options = CreateOptions(HttpLoggingFields.All);
        options.RequestHeaders.Add("Authorization");
        options.ResponseHeaders.Add("Set-Cookie");

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures?.Count());
    }

    #endregion
}

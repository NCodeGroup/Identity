// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Options;

public class BffAntiforgeryCookieValidatorTests : BaseTests
{
    private static BffAntiforgeryCookieValidator CreateValidator() => new();

    private static AntiforgeryOptions CreateValidOptions() =>
        new()
        {
            Cookie =
            {
                Name = BffDefaults.AntiforgeryCookieName,
                SecurePolicy = CookieSecurePolicy.Always,
                Path = "/",
            },
        };

    #region Validate Tests

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("antiforgery")]
    [InlineData("__host-antiforgery")]
    [InlineData("__Secure-antiforgery")]
    public void Validate_WhenNameIsNotHostPrefixed_Fails(string name)
    {
        var options = CreateValidOptions();
        options.Cookie.Name = name;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "The antiforgery cookie's name must start with '__Host-'.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData(CookieSecurePolicy.None)]
    [InlineData(CookieSecurePolicy.SameAsRequest)]
    public void Validate_WhenSecurePolicyIsNotAlways_Fails(CookieSecurePolicy policy)
    {
        var options = CreateValidOptions();
        options.Cookie.SecurePolicy = policy;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("The antiforgery cookie must be Secure always.", result.Failures ?? []);
    }

    [Theory]
    [InlineData("/app")]
    [InlineData(null)]
    public void Validate_WhenPathIsNotRoot_Fails(string? path)
    {
        var options = CreateValidOptions();
        options.Cookie.Path = path;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("The antiforgery cookie's path must be '/'.", result.Failures ?? []);
    }

    [Fact]
    public void Validate_WhenDomainIsSet_Fails()
    {
        var options = CreateValidOptions();
        options.Cookie.Domain = "example.com";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("The antiforgery cookie must not set a domain.", result.Failures ?? []);
    }

    #endregion
}

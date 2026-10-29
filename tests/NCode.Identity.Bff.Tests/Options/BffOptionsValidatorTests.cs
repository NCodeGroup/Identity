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

using Microsoft.Extensions.Hosting;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Options;

public class BffOptionsValidatorTests : BaseTests
{
    private BffOptionsValidator CreateValidator(string environmentName = "Production")
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return new BffOptionsValidator(mockEnvironment.Object);
    }

    private static BffOptions CreateValidOptions() =>
        new()
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            ReturnUrls = ["/"],
            TrustedOrigins = ["https://app.example.com"],
        };

    #region Validate Tests

    [Fact]
    public void Validate_WhenValidBaseline_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("op.example.com")]
    [InlineData("http://op.example.com")]
    [InlineData("https://op .example.com")]
    public void Validate_WhenAuthorityInvalid_Fails(string authority)
    {
        var options = CreateValidOptions();
        options.Authority = authority;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Authority"));
    }

    [Fact]
    public void Validate_WhenHttpAuthorityInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        options.Authority = "http://localhost:5000";

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenClientIdBlank_Fails()
    {
        var options = CreateValidOptions();
        options.ClientId = " ";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:ClientId"));
    }

    [Fact]
    public void Validate_WhenClientAuthenticationMethodBlank_Fails()
    {
        var options = CreateValidOptions();
        options.ClientAuthentication.Method = "";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:ClientAuthentication:Method"));
    }

    [Fact]
    public void Validate_WhenReturnUrlsEmpty_Fails()
    {
        var options = CreateValidOptions();
        options.ReturnUrls = [];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:ReturnUrls"));
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("//evil.com")]
    [InlineData("/orders?x=1")]
    public void Validate_WhenReturnUrlEntryInvalid_Fails(string entry)
    {
        var options = CreateValidOptions();
        options.ReturnUrls = [entry];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:ReturnUrls"));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("//evil.com")]
    [InlineData("")]
    public void Validate_WhenErrorPathInvalid_Fails(string errorPath)
    {
        var options = CreateValidOptions();
        options.ErrorPath = errorPath;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:ErrorPath"));
    }

    [Theory]
    [InlineData("bff")]
    [InlineData("/bff/")]
    [InlineData("/")]
    [InlineData("/a/../b")]
    public void Validate_WhenPathPrefixInvalid_Fails(string pathPrefix)
    {
        var options = CreateValidOptions();
        options.PathPrefix = pathPrefix;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:PathPrefix"));
    }

    [Theory]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("http://app.example.com")]
    [InlineData("app.example.com")]
    public void Validate_WhenTrustedOriginInvalid_Fails(string origin)
    {
        var options = CreateValidOptions();
        options.TrustedOrigins = [origin];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:TrustedOrigins"));
    }

    [Fact]
    public void Validate_WhenHttpTrustedOriginInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        options.TrustedOrigins = ["http://localhost:3000"];

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    #endregion
}

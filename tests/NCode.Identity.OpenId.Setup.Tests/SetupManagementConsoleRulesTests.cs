// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupManagementConsoleRulesTests
{
    private const string Jwks = """{"keys":[{"kty":"oct"}]}""";

    #region Helpers

    private static SetupManagementConsoleOptions CreateConsole(string? baseUrl) =>
        new()
        {
            ClientId = SetupManagementConsoleOptions.DefaultClientId,
            BaseUrl = baseUrl,
            Jwks = Jwks,
        };

    #endregion

    #region Validate Tests

    [Theory]
    [InlineData("https://console.example.com")]
    [InlineData("https://console.example.com/")]
    [InlineData("https://console.example.com:8443")]
    public void Validate_HttpsOrigin_Succeeds(string baseUrl)
    {
        var failures = SetupManagementConsoleRules.Validate(
            CreateConsole(baseUrl),
            namesConfigurationKeys: true
        );

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("http://console.example.com")]
    [InlineData("ftp://console.example.com")]
    public void Validate_NotHttps_Fails(string baseUrl)
    {
        var failures = SetupManagementConsoleRules.Validate(
            CreateConsole(baseUrl),
            namesConfigurationKeys: true
        );

        var failure = Assert.Single(failures);
        Assert.Contains("'Setup:ManagementConsole:BaseUrl'", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("console.example.com")]
    [InlineData("/console")]
    [InlineData("//console.example.com")]
    [InlineData("https://console.example.com with space")]
    [InlineData(" https://console.example.com")]
    public void Validate_NotAnAbsoluteUri_Fails(string baseUrl)
    {
        var failures = SetupManagementConsoleRules.Validate(
            CreateConsole(baseUrl),
            namesConfigurationKeys: true
        );

        var failure = Assert.Single(failures);
        Assert.Contains("'Setup:ManagementConsole:BaseUrl'", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://console.example.com/path")]
    [InlineData("https://console.example.com?a=b")]
    [InlineData("https://console.example.com#fragment")]
    [InlineData("https://user@console.example.com")]
    public void Validate_NotAnOrigin_Fails(string baseUrl)
    {
        var failures = SetupManagementConsoleRules.Validate(
            CreateConsole(baseUrl),
            namesConfigurationKeys: false
        );

        var failure = Assert.Single(failures);
        Assert.Contains("The console URL", failure, StringComparison.Ordinal);
    }

    #endregion
}

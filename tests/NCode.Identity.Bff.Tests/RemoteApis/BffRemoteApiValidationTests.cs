// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiValidationTests : BaseTests
{
    private const string Key = "Bff:Frontends:web:RemoteApis:orders";

    private static BffRemoteApiOptions CreateValidApi() =>
        new() { Path = "/api/orders", Destination = "https://api.example.com/v1" };

    private static List<string> Validate(BffRemoteApiOptions? api, bool isLocalDevelopment = false)
    {
        List<string> failures = [];
        BffRemoteApiValidation.Validate(Key, api, isLocalDevelopment, failures);
        return failures;
    }

    #region Validate Tests

    [Fact]
    public void Validate_ValidApi_HasNoFailures()
    {
        var failures = Validate(CreateValidApi());

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_NullApi_Fails()
    {
        var failures = Validate(null);

        Assert.Equal([$"{Key} must be set."], failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("api/orders")]
    [InlineData("/api/orders/")]
    [InlineData("/api//orders")]
    [InlineData("/api/../orders")]
    [InlineData("/api/./orders")]
    [InlineData("/api/{id}")]
    [InlineData("/api/orders?x=1")]
    [InlineData("/api/or%20ders")]
    [InlineData("/api/*")]
    public void Validate_InvalidPath_Fails(string path)
    {
        var api = CreateValidApi();
        api.Path = path;

        var failures = Validate(api);

        Assert.Contains(failures, x => x.StartsWith($"{Key}:Path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/a")]
    [InlineData("/api/v1.0/orders_list-x~y")]
    public void Validate_ValidPath_HasNoFailures(string path)
    {
        var api = CreateValidApi();
        api.Path = path;

        var failures = Validate(api);

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("/relative")]
    [InlineData("http://api.example.com")]
    [InlineData("https://api.example.com/v1?x=1")]
    [InlineData("https://api.example.com/v1?")]
    [InlineData("https://api.example.com/v1#top")]
    [InlineData("ftp://api.example.com")]
    public void Validate_InvalidDestination_Fails(string destination)
    {
        var api = CreateValidApi();
        api.Destination = destination;

        var failures = Validate(api);

        Assert.Contains(
            failures,
            x => x.StartsWith($"{Key}:Destination '{destination}'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_DestinationWithUserInfo_WithholdsTheUrl()
    {
        var api = CreateValidApi();
        api.Destination = "https://user:secret@api.example.com";

        var failures = Validate(api);

        var failure = Assert.Single(failures);
        Assert.DoesNotContain("secret", failure, StringComparison.Ordinal);
        Assert.Contains("(withheld because it contains '@')", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_HttpDestinationWhenTheSwitchIsOn_HasNoFailures()
    {
        var api = CreateValidApi();
        api.Destination = "http://localhost:5001";

        var failures = Validate(api, isLocalDevelopment: true);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_HttpDestinationWhenTheSwitchIsOff_Fails()
    {
        var api = CreateValidApi();
        api.Destination = "http://localhost:5001";

        var failures = Validate(api, isLocalDevelopment: false);

        var failure = Assert.Single(failures);
        Assert.StartsWith(
            $"{Key}:Destination 'http://localhost:5001'",
            failure,
            StringComparison.Ordinal
        );
        Assert.Contains("LocalDevelopment:Enabled", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_BlankTokenRequirement_Fails(string requirement)
    {
        var api = CreateValidApi();
        api.TokenRequirement = requirement;

        var failures = Validate(api);

        Assert.Equal([$"{Key}:TokenRequirement must not be blank."], failures);
    }

    [Fact]
    public void Validate_NullAllowedMethods_Fails()
    {
        var api = CreateValidApi();
        api.AllowedMethods = null!;

        var failures = Validate(api);

        Assert.Equal([$"{Key}:AllowedMethods must be set."], failures);
    }

    [Fact]
    public void Validate_BlankAllowedMethod_Fails()
    {
        var api = CreateValidApi();
        api.AllowedMethods = ["GET", " "];

        var failures = Validate(api);

        Assert.Equal([$"{Key}:AllowedMethods must not contain a blank entry."], failures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveTimeout_Fails(int seconds)
    {
        var api = CreateValidApi();
        api.Timeout = TimeSpan.FromSeconds(seconds);

        var failures = Validate(api);

        Assert.Equal([$"{Key}:Timeout must be greater than zero."], failures);
    }

    #endregion

    #region Overlaps Tests

    [Theory]
    [InlineData("/api/orders", "/api/orders", true)]
    [InlineData("/api/orders", "/API/ORDERS", true)]
    [InlineData("/api", "/api/orders", true)]
    [InlineData("/api/orders", "/api", true)]
    [InlineData("/api/orders", "/api/orders2", false)]
    [InlineData("/api/order", "/api/orders", false)]
    [InlineData("/bff", "/api", false)]
    public void Overlaps_ComparesWholeSegments(string first, string second, bool expected)
    {
        Assert.Equal(expected, BffRemoteApiValidation.Overlaps(first, second));
    }

    #endregion
}

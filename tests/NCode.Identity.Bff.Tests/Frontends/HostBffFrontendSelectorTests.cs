// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class HostBffFrontendSelectorTests : BaseTests
{
    private readonly HostBffFrontendSelector _selector = new();

    private static BffFrontendSelection CreateSelection(
        string[] values,
        string publicOrigin = "https://app.example.com"
    ) => new(values, publicOrigin, ["/bff"]);

    private static HttpRequest CreateRequest(string host)
    {
        var request = new DefaultHttpContext().Request;
        request.Host = new HostString(host);
        return request;
    }

    #region Name Tests

    [Fact]
    public void Name_IsTheHostRuleName()
    {
        Assert.Equal(BffFrontendSelectorNames.Host, _selector.Name);
    }

    #endregion

    #region IsMatch Tests

    [Fact]
    public void IsMatch_HostWithoutPort_MatchesAnyPort()
    {
        var selection = CreateSelection(["app.example.com"]);

        Assert.True(_selector.IsMatch(CreateRequest("app.example.com"), selection));
        Assert.True(_selector.IsMatch(CreateRequest("app.example.com:8443"), selection));
    }

    [Fact]
    public void IsMatch_HostWithPort_MatchesOnlyThatPort()
    {
        var selection = CreateSelection(["app.example.com:8443"]);

        Assert.True(_selector.IsMatch(CreateRequest("app.example.com:8443"), selection));
        Assert.False(_selector.IsMatch(CreateRequest("app.example.com:9443"), selection));
        Assert.False(_selector.IsMatch(CreateRequest("app.example.com"), selection));
    }

    [Fact]
    public void IsMatch_HostDiffersInCase_Matches()
    {
        var selection = CreateSelection(["App.Example.com"]);

        Assert.True(_selector.IsMatch(CreateRequest("APP.example.COM"), selection));
    }

    [Fact]
    public void IsMatch_OtherHost_ReturnsFalse()
    {
        var selection = CreateSelection(["app.example.com"]);

        Assert.False(_selector.IsMatch(CreateRequest("other.example.com"), selection));
        Assert.False(_selector.IsMatch(CreateRequest("sub.app.example.com"), selection));
    }

    [Fact]
    public void IsMatch_AnyValueMatches_ReturnsTrue()
    {
        var selection = CreateSelection(["one.example.com", "two.example.com"]);

        Assert.True(_selector.IsMatch(CreateRequest("two.example.com"), selection));
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ValidSelection_ReturnsNoFailures()
    {
        var selection = CreateSelection(["app.example.com", "app.example.com:8443"]);

        var failures = _selector.Validate(selection);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_IPv4Value_ReturnsNoFailures()
    {
        var selection = CreateSelection(["127.0.0.1:5001"], "https://127.0.0.1:5001");

        var failures = _selector.Validate(selection);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_NoValues_ReturnsFailure()
    {
        var selection = CreateSelection([]);

        var failures = _selector.Validate(selection);

        Assert.Equal(["Values must name at least one host."], failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("app example.com")]
    [InlineData("https://app.example.com")]
    [InlineData("app.example.com/path")]
    [InlineData("app.example.com:0")]
    [InlineData("app.example.com:65536")]
    [InlineData("app.example.com:abc")]
    [InlineData("app.example.com:")]
    [InlineData("[::1]")]
    public void Validate_InvalidValue_ReturnsFailure(string value)
    {
        var selection = CreateSelection(["app.example.com", value]);

        var failures = _selector.Validate(selection);

        Assert.Equal(
            [
                $"Values entry '{value}' must be a host name with an optional port, such as 'app.example.com' or 'app.example.com:8443'.",
            ],
            failures
        );
    }

    [Fact]
    public void Validate_PublicOriginHostMissing_ReturnsFailure()
    {
        var selection = CreateSelection(["other.example.com"]);

        var failures = _selector.Validate(selection);

        Assert.Equal(
            [
                "Values must include the host of PublicOrigin 'https://app.example.com', so the frontend's callback selects it.",
            ],
            failures
        );
    }

    [Fact]
    public void Validate_PublicOriginPortDiffers_ReturnsFailure()
    {
        var selection = CreateSelection(["app.example.com:8443"], "https://app.example.com");

        var failures = _selector.Validate(selection);

        Assert.Single(failures);
    }

    [Fact]
    public void Validate_PublicOriginPortMatches_ReturnsNoFailures()
    {
        var selection = CreateSelection(["APP.example.com:8443"], "https://app.example.com:8443");

        var failures = _selector.Validate(selection);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_PublicOriginNotAbsolute_SkipsTheOriginCheck()
    {
        var selection = CreateSelection(["other.example.com"], "not a uri");

        var failures = _selector.Validate(selection);

        Assert.Empty(failures);
    }

    #endregion

    #region Overlaps Tests

    [Fact]
    public void Overlaps_SameHost_ReturnsTrue()
    {
        var first = CreateSelection(["app.example.com"]);
        var second = CreateSelection(["APP.example.com"]);

        Assert.True(_selector.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_SameHostAndPort_ReturnsTrue()
    {
        var first = CreateSelection(["app.example.com:8443"]);
        var second = CreateSelection(["app.example.com:8443"]);

        Assert.True(_selector.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_SameHostOnePortAbsent_ReturnsTrue()
    {
        var first = CreateSelection(["app.example.com:8443"]);
        var second = CreateSelection(["app.example.com"]);

        Assert.True(_selector.Overlaps(first, second));
        Assert.True(_selector.Overlaps(second, first));
    }

    [Fact]
    public void Overlaps_SameHostDifferentPorts_ReturnsFalse()
    {
        var first = CreateSelection(["app.example.com:8443"]);
        var second = CreateSelection(["app.example.com:9443"]);

        Assert.False(_selector.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_DifferentHosts_ReturnsFalse()
    {
        var first = CreateSelection(["one.example.com"]);
        var second = CreateSelection(["two.example.com"]);

        Assert.False(_selector.Overlaps(first, second));
    }

    #endregion
}

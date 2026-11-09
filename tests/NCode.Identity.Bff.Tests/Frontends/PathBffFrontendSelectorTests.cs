// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class PathBffFrontendSelectorTests : BaseTests
{
    private PathBffFrontendSelector Selector { get; } = new();

    private static BffFrontendSelection CreateSelection(string[] values, string[]? paths = null) =>
        new(values, "https://app.example.com", paths ?? []);

    private static HttpRequest CreateRequest(string path)
    {
        var request = new DefaultHttpContext().Request;
        request.Path = path;
        return request;
    }

    #region Name Tests

    [Fact]
    public void Name_IsThePathRuleName()
    {
        Assert.Equal(BffFrontendSelectorNames.Path, Selector.Name);
    }

    #endregion

    #region IsMatch Tests

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/")]
    [InlineData("/admin/users")]
    [InlineData("/ADMIN/users")]
    public void IsMatch_PathUnderPrefix_ReturnsTrue(string path)
    {
        var selection = CreateSelection(["/admin"]);

        Assert.True(Selector.IsMatch(CreateRequest(path), selection));
    }

    [Theory]
    [InlineData("/administrator")]
    [InlineData("/administrator/users")]
    [InlineData("/")]
    [InlineData("/other")]
    public void IsMatch_PathOutsidePrefix_ReturnsFalse(string path)
    {
        var selection = CreateSelection(["/admin"]);

        Assert.False(Selector.IsMatch(CreateRequest(path), selection));
    }

    [Fact]
    public void IsMatch_AnyValueMatches_ReturnsTrue()
    {
        var selection = CreateSelection(["/one", "/two"]);

        Assert.True(Selector.IsMatch(CreateRequest("/two/page"), selection));
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ValidSelection_ReturnsNoFailures()
    {
        var selection = CreateSelection(["/admin", "/reports"], ["/admin/bff", "/reports/api"]);

        var failures = Selector.Validate(selection);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_NoValues_ReturnsFailure()
    {
        var selection = CreateSelection([]);

        var failures = Selector.Validate(selection);

        Assert.Equal(["Values must name at least one path."], failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("admin")]
    [InlineData("/admin/")]
    [InlineData("/admin?x=1")]
    [InlineData("/admin/../other")]
    [InlineData("//admin")]
    [InlineData("/ad#min")]
    public void Validate_InvalidValue_ReturnsFailure(string value)
    {
        var selection = CreateSelection(["/valid", value], ["/valid/bff"]);

        var failures = Selector.Validate(selection);

        Assert.Equal(
            [
                $"Values entry '{value}' must be a well-formed local path that starts with '/' and does not end with '/'.",
            ],
            failures
        );
    }

    [Fact]
    public void Validate_PathNotUnderAnyValue_ReturnsFailurePerPath()
    {
        var selection = CreateSelection(["/admin"], ["/admin/bff", "/administrator/api", "/other"]);

        var failures = Selector.Validate(selection);

        Assert.Equal(
            [
                "Values must cover '/administrator/api', so its requests select the frontend.",
                "Values must cover '/other', so its requests select the frontend.",
            ],
            failures
        );
    }

    [Fact]
    public void Validate_PathDiffersInCase_ReturnsNoFailures()
    {
        var selection = CreateSelection(["/Admin"], ["/admin/bff"]);

        var failures = Selector.Validate(selection);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_PathEqualsValue_ReturnsNoFailures()
    {
        var selection = CreateSelection(["/admin"], ["/admin"]);

        var failures = Selector.Validate(selection);

        Assert.Empty(failures);
    }

    #endregion

    #region Overlaps Tests

    [Fact]
    public void Overlaps_SameValue_ReturnsTrue()
    {
        var first = CreateSelection(["/admin"]);
        var second = CreateSelection(["/ADMIN"]);

        Assert.True(Selector.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_OneValueCoversTheOther_ReturnsTrue()
    {
        var first = CreateSelection(["/admin"]);
        var second = CreateSelection(["/admin/reports"]);

        Assert.True(Selector.Overlaps(first, second));
        Assert.True(Selector.Overlaps(second, first));
    }

    [Fact]
    public void Overlaps_SiblingPrefixesOfOneSegment_ReturnsFalse()
    {
        var first = CreateSelection(["/admin"]);
        var second = CreateSelection(["/administrator"]);

        Assert.False(Selector.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_DisjointValues_ReturnsFalse()
    {
        var first = CreateSelection(["/one"]);
        var second = CreateSelection(["/two", "/three"]);

        Assert.False(Selector.Overlaps(first, second));
    }

    #endregion
}

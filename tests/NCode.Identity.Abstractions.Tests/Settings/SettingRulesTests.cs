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

namespace NCode.Identity.Settings;

public class SettingRulesTests
{
    public static TheoryData<string[], string> OneOfDescriptions { get; } =
        new()
        {
            { [], "a value from an empty set" },
            { ["fapi2"], "'fapi2'" },
            { ["poll", "ping", "push"], "one of 'poll', 'ping', or 'push'" },
            { ["a", "b", "a"], "one of 'a' or 'b'" },
        };

    #region Any Tests

    [Fact]
    public void Any_WhenValidated_AcceptsEveryValue()
    {
        var rule = SettingRules.Any<string>();

        var valid = rule.TryValidate(string.Empty, out var failure);

        Assert.True(valid);
        Assert.Null(failure);
        Assert.Equal("any value", rule.Description);
    }

    #endregion

    #region Must Tests

    [Fact]
    public void Must_WhenPredicateHolds_Accepts()
    {
        var rule = SettingRules.Must<int>("an even number", value => value % 2 == 0);

        Assert.True(rule.TryValidate(4, out _));
    }

    [Fact]
    public void Must_WhenPredicateFails_ReportsTheDescription()
    {
        var rule = SettingRules.Must<int>("an even number", value => value % 2 == 0);

        var valid = rule.TryValidate(3, out var failure);

        Assert.False(valid);
        Assert.Equal("must be an even number", failure);
    }

    [Fact]
    public void Must_WhenPredicateConsultsChangingState_EvaluatesItOnEveryValidation()
    {
        var registered = new HashSet<string>(StringComparer.Ordinal);
        var rule = SettingRules.Must<string>("a registered name", registered.Contains);

        var before = rule.TryValidate("fapi2", out _);
        registered.Add("fapi2");
        var after = rule.TryValidate("fapi2", out _);

        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public void Must_WhenDescriptionIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => SettingRules.Must<int>(string.Empty, _ => true));
    }

    #endregion

    #region OneOf Tests

    [Fact]
    public void OneOf_WhenValueIsListed_Accepts()
    {
        var rule = SettingRules.OneOf("public", "pairwise");

        Assert.True(rule.TryValidate("pairwise", out _));
    }

    [Fact]
    public void OneOf_WhenValueDiffersOnlyByCase_Rejects()
    {
        var rule = SettingRules.OneOf("public", "pairwise");

        var valid = rule.TryValidate("Pairwise", out var failure);

        Assert.False(valid);
        Assert.Equal("must be one of 'public' or 'pairwise'", failure);
    }

    [Theory]
    [MemberData(nameof(OneOfDescriptions))]
    public void OneOf_Description_ListsTheDistinctValuesInOrder(string[] values, string expected)
    {
        var rule = SettingRules.OneOf(values);

        Assert.Equal(expected, rule.Description);
    }

    #endregion

    #region NonEmpty Tests

    [Fact]
    public void NonEmpty_WhenEmpty_Rejects()
    {
        var rule = SettingRules.NonEmpty();

        var valid = rule.TryValidate(string.Empty, out var failure);

        Assert.False(valid);
        Assert.Equal("must be a non-empty value", failure);
    }

    [Fact]
    public void NonEmpty_WhenNotEmpty_Accepts()
    {
        Assert.True(SettingRules.NonEmpty().TryValidate("x", out _));
    }

    #endregion

    #region EmptyOr Tests

    [Theory]
    [InlineData("")]
    [InlineData("fapi2")]
    public void EmptyOr_WhenEmptyOrSatisfyingTheRule_Accepts(string value)
    {
        var rule = SettingRules.EmptyOr(SettingRules.OneOf("fapi2"));

        Assert.True(rule.TryValidate(value, out _));
    }

    [Fact]
    public void EmptyOr_WhenNeither_RejectsWithBothAlternatives()
    {
        var rule = SettingRules.EmptyOr(SettingRules.OneOf("fapi2"));

        var valid = rule.TryValidate("fapi-2", out var failure);

        Assert.False(valid);
        Assert.Equal("must be empty or 'fapi2'", failure);
    }

    #endregion

    #region AtLeast Tests

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void AtLeast_ComparesInclusively(int value, bool expected)
    {
        var rule = SettingRules.AtLeast(1);

        Assert.Equal(expected, rule.TryValidate(value, out _));
    }

    [Fact]
    public void AtLeast_WhenRejected_DescribesTheMinimum()
    {
        var rule = SettingRules.AtLeast(TimeSpan.FromSeconds(1));

        rule.TryValidate(TimeSpan.FromMinutes(-5), out var failure);

        Assert.Equal("must be at least 00:00:01", failure);
    }

    #endregion

    #region Between Tests

    [Theory]
    [InlineData(8, true)]
    [InlineData(32, true)]
    [InlineData(7, false)]
    [InlineData(33, false)]
    public void Between_ComparesInclusively(int value, bool expected)
    {
        var rule = SettingRules.Between(8, 32);

        Assert.Equal(expected, rule.TryValidate(value, out _));
    }

    [Fact]
    public void Between_Description_NamesBothBounds()
    {
        Assert.Equal("between 8 and 32", SettingRules.Between(8, 32).Description);
    }

    [Fact]
    public void Between_WhenMinimumExceedsMaximum_Throws()
    {
        Assert.Throws<ArgumentException>(() => SettingRules.Between(2, 1));
    }

    #endregion

    #region Each Tests

    [Fact]
    public void Each_WhenEveryValueSatisfiesTheRule_Accepts()
    {
        var rule = SettingRules.Each(SettingRules.OneOf("S256", "plain"));

        Assert.True(rule.TryValidate(["S256", "plain"], out _));
    }

    [Fact]
    public void Each_WhenListIsEmpty_Accepts()
    {
        var rule = SettingRules.Each(SettingRules.OneOf("S256"));

        Assert.True(rule.TryValidate([], out _));
    }

    [Fact]
    public void Each_WhenAValueFails_NamesTheFirstOffendingValue()
    {
        var rule = SettingRules.Each(SettingRules.OneOf("S256", "plain"));

        var valid = rule.TryValidate(["S256", "S512", "S384"], out var failure);

        Assert.False(valid);
        Assert.Equal(
            "must list only values that are one of 'S256' or 'plain'; 'S512' is not",
            failure
        );
    }

    [Fact]
    public void Each_WhenAValueIsNull_RejectsIt()
    {
        var rule = SettingRules.Each(SettingRules.Any<string>());

        var valid = rule.TryValidate(["a", null!], out var failure);

        Assert.False(valid);
        Assert.Equal("must list only values that are any value, not null", failure);
    }

    [Fact]
    public void Each_Description_WrapsTheElementRule()
    {
        var rule = SettingRules.Each(SettingRules.NonEmpty());

        Assert.Equal("a list of values that are each a non-empty value", rule.Description);
    }

    #endregion
}

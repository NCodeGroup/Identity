#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

public class SettingDescriptorTests
{
    #region ValueType Property Tests

    [Fact]
    public void ValueType_WhenStringValue_ReturnsStringType()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        Assert.Equal(typeof(string), descriptor.ValueType);
    }

    #endregion

    #region HasDefault Property Tests

    [Fact]
    public void HasDefault_WhenDefaultNotSet_ReturnsFalse()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        Assert.False(descriptor.HasDefault);
    }

    [Fact]
    public void HasDefault_WhenDefaultSet_ReturnsTrue()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            Default = "value",
        };

        Assert.True(descriptor.HasDefault);
    }

    #endregion

    #region HasLatticeMerge Property Tests

    [Fact]
    public void HasLatticeMerge_WhenMergedWithMinOrMax_ReturnsTrue()
    {
        var min = new SettingDescriptor<TimeSpan>
        {
            Name = "min",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Min,
        };
        var max = new SettingDescriptor<TimeSpan>
        {
            Name = "max",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Max,
        };

        Assert.True(min.HasLatticeMerge);
        Assert.True(max.HasLatticeMerge);
    }

    [Fact]
    public void HasLatticeMerge_WhenMergedWithAndOrOr_ReturnsTrue()
    {
        var and = new SettingDescriptor<bool>
        {
            Name = "and",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.And,
        };
        var or = new SettingDescriptor<bool>
        {
            Name = "or",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Or,
        };

        Assert.True(and.HasLatticeMerge);
        Assert.True(or.HasLatticeMerge);
    }

    [Fact]
    public void HasLatticeMerge_WhenMergedWithIntersectOrUnion_ReturnsTrue()
    {
        var intersect = new SettingDescriptor<IReadOnlyCollection<string>>
        {
            Name = "intersect",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Intersect,
        };
        var union = new SettingDescriptor<IReadOnlyCollection<string>>
        {
            Name = "union",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Union,
        };

        Assert.True(intersect.HasLatticeMerge);
        Assert.True(union.HasLatticeMerge);
    }

    [Fact]
    public void HasLatticeMerge_WhenMergedWithAProjection_ReturnsFalse()
    {
        var keep = new SettingDescriptor<string>
        {
            Name = "keep",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Keep,
        };
        var replace = new SettingDescriptor<string>
        {
            Name = "replace",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Replace,
        };

        Assert.False(keep.HasLatticeMerge);
        Assert.False(replace.HasLatticeMerge);
    }

    [Fact]
    public void HasLatticeMerge_WhenMergedWithTheDefaultOrAnotherFunction_ReturnsFalse()
    {
        var byDefault = new SettingDescriptor<bool> { Name = "default", AppliesTo = ["client"] };
        var custom = new SettingDescriptor<bool>
        {
            Name = "custom",
            AppliesTo = ["client"],
            OnMerge = (current, other) => SettingMerge.Or(current, other),
        };

        Assert.False(byDefault.HasLatticeMerge);
        Assert.False(custom.HasLatticeMerge);
    }

    #endregion

    #region HasMeetMerge Property Tests

    [Fact]
    public void HasMeetMerge_WhenMergedWithMinAndOrIntersect_ReturnsTrue()
    {
        var min = new SettingDescriptor<TimeSpan>
        {
            Name = "min",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Min,
        };
        var and = new SettingDescriptor<bool>
        {
            Name = "and",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.And,
        };
        var intersect = new SettingDescriptor<IReadOnlyCollection<string>>
        {
            Name = "intersect",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Intersect,
        };

        Assert.True(min.HasMeetMerge);
        Assert.True(and.HasMeetMerge);
        Assert.True(intersect.HasMeetMerge);
    }

    [Fact]
    public void HasMeetMerge_WhenMergedWithMaxOrOrUnion_ReturnsFalse()
    {
        var max = new SettingDescriptor<TimeSpan>
        {
            Name = "max",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Max,
        };
        var or = new SettingDescriptor<bool>
        {
            Name = "or",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Or,
        };
        var union = new SettingDescriptor<IReadOnlyCollection<string>>
        {
            Name = "union",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Union,
        };

        Assert.False(max.HasMeetMerge);
        Assert.False(or.HasMeetMerge);
        Assert.False(union.HasMeetMerge);
        Assert.True(max.HasLatticeMerge);
        Assert.True(or.HasLatticeMerge);
        Assert.True(union.HasLatticeMerge);
    }

    [Fact]
    public void HasMeetMerge_WhenMergedWithAProjectionOrAnotherFunction_ReturnsFalse()
    {
        var replace = new SettingDescriptor<string>
        {
            Name = "replace",
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Replace,
        };
        var byDefault = new SettingDescriptor<bool> { Name = "default", AppliesTo = ["client"] };
        var custom = new SettingDescriptor<TimeSpan>
        {
            Name = "custom",
            AppliesTo = ["client"],
            OnMerge = (current, other) => SettingMerge.Min(current, other),
        };

        Assert.False(replace.HasMeetMerge);
        Assert.False(byDefault.HasMeetMerge);
        Assert.False(custom.HasMeetMerge);
    }

    #endregion

    #region Default Property Tests

    [Fact]
    public void Default_WhenNotSet_Throws()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        Assert.Throws<InvalidOperationException>(() => descriptor.Default);
    }

    [Fact]
    public void Default_WhenSet_ReturnsValue()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            Default = "value",
        };

        Assert.Equal("value", descriptor.Default);
        Assert.Equal("value", descriptor.DefaultOrNull);
    }

    #endregion

    #region Key Property Tests

    [Fact]
    public void Key_WhenAccessed_ReturnsKeyWithName()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        var key = descriptor.Key;

        Assert.Equal("example", key.SettingName);
        Assert.Equal(typeof(string), key.ValueType);
    }

    [Fact]
    public void ImplicitConversion_ToSettingKey_UsesName()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        SettingKey<string> key = descriptor;

        Assert.Equal("example", key.SettingName);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithTypedValue_ReturnsSettingWithValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        var setting = descriptor.Create("value");

        Assert.Equal("value", setting.Value);
        Assert.Same(descriptor, setting.Descriptor);
    }

    [Fact]
    public void Create_WithBoxedValue_ReturnsSettingWithValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        var setting = descriptor.Create((object)"value");

        Assert.Equal("value", setting.GetValue());
    }

    [Fact]
    public void CreateDefault_WhenDefaultSet_ReturnsSettingWithDefault()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            Default = "value",
        };

        var setting = descriptor.CreateDefault();

        Assert.Equal("value", setting.GetValue());
    }

    [Fact]
    public void CreateDefault_WhenNoDefault_Throws()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        Assert.Throws<InvalidOperationException>(() => descriptor.CreateDefault());
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_WhenDefaultBehavior_ReturnsOtherValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };
        var current = descriptor.Create("current");
        var other = descriptor.Create("other");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("other", merged.Value);
    }

    [Fact]
    public void Merge_WhenCustomOnMerge_UsesCustomLogic()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            OnMerge = (current, other) => current + other,
        };
        var current = descriptor.Create("a");
        var other = descriptor.Create("b");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("ab", merged.Value);
    }

    [Fact]
    public void Merge_WithBoxedSettings_ReturnsOtherValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };
        Setting current = descriptor.Create("current");
        Setting other = descriptor.Create("other");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("other", merged.GetValue());
    }

    #endregion

    #region Format Tests

    [Fact]
    public void Format_WhenDefaultBehavior_ReturnsValueAsIs()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };
        var setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("value", formatted);
    }

    [Fact]
    public void Format_WhenCustomOnFormat_UsesCustomLogic()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            OnFormat = setting => setting.Value.ToUpperInvariant(),
        };
        var setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("VALUE", formatted);
    }

    [Fact]
    public void Format_WithBoxedSetting_ReturnsValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };
        Setting setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("value", formatted);
    }

    #endregion

    #region AppliesTo Property Tests

    [Fact]
    public void AppliesTo_WhenSet_RoundTripsTheScopeNames()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["tenant", "organization"],
        };

        Assert.Equal<string>(["tenant", "organization"], descriptor.AppliesTo);
    }

    #endregion

    #region IsDiscoverable Property Tests

    [Fact]
    public void IsDiscoverable_WhenSet_RoundTrips()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            IsDiscoverable = true,
        };

        Assert.True(descriptor.IsDiscoverable);
    }

    #endregion

    #region Rule Property Tests

    [Fact]
    public void Rule_WhenNotSet_AcceptsEveryValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", AppliesTo = ["client"] };

        Assert.Equal("any value", descriptor.Rule.Description);
    }

    #endregion

    #region TryValidate Tests

    [Fact]
    public void TryValidate_WhenValueSatisfiesTheRule_ReturnsTrue()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            Rule = SettingRules.OneOf("public", "pairwise"),
        };

        var valid = descriptor.TryValidate("public", out var failure);

        Assert.True(valid);
        Assert.Null(failure);
    }

    [Fact]
    public void TryValidate_WhenValueIsOutsideTheDomain_ReturnsTheRuleFailure()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            AppliesTo = ["client"],
            Rule = SettingRules.OneOf("public", "pairwise"),
        };

        var valid = descriptor.TryValidate("pairwse", out var failure);

        Assert.False(valid);
        Assert.Equal("must be one of 'public' or 'pairwise'", failure);
    }

    [Fact]
    public void TryValidate_WithBoxedValue_EvaluatesTheRule()
    {
        SettingDescriptor descriptor = new SettingDescriptor<TimeSpan>
        {
            Name = "example",
            AppliesTo = ["client"],
            Rule = SettingRules.AtLeast(TimeSpan.Zero),
        };

        var valid = descriptor.TryValidate(TimeSpan.FromMinutes(-1), out var failure);

        Assert.False(valid);
        Assert.NotNull(failure);
    }

    #endregion
}

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

using Xunit;

namespace NCode.Identity.Settings;

public class SettingMergeTests
{
    #region Projections

    [Fact]
    public void Keep_ReturnsParentValue()
    {
        Assert.Equal("parent", SettingMerge.Keep("parent", "child"));
    }

    [Fact]
    public void Replace_ReturnsChildValue()
    {
        Assert.Equal("child", SettingMerge.Replace("parent", "child"));
    }

    #endregion

    #region Boolean meet/join

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void And_IsTheBooleanMeet(bool current, bool other, bool expected)
    {
        Assert.Equal(expected, SettingMerge.And(current, other));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Or_IsTheBooleanJoin(bool current, bool other, bool expected)
    {
        Assert.Equal(expected, SettingMerge.Or(current, other));
    }

    [Fact]
    public void And_AsCeiling_ParentFalseClampsChildTrue()
    {
        // allow-flag polarity: a parent that forbids (false) cannot be re-allowed by a child.
        Assert.False(SettingMerge.And(current: false, other: true));
    }

    [Fact]
    public void Or_AsFloor_ParentTrueOverridesChildFalse()
    {
        // require-flag polarity: a parent that requires (true) cannot be un-required by a child.
        Assert.True(SettingMerge.Or(current: true, other: false));
    }

    #endregion

    #region Ordered meet/join

    [Fact]
    public void Min_ReturnsLesser_AndClampsChildToParentCeiling()
    {
        var parent = TimeSpan.FromMinutes(5);
        var longerChild = TimeSpan.FromHours(1);
        var shorterChild = TimeSpan.FromMinutes(2);

        Assert.Equal(parent, SettingMerge.Min(parent, longerChild));
        Assert.Equal(shorterChild, SettingMerge.Min(parent, shorterChild));
    }

    [Fact]
    public void Max_ReturnsGreater()
    {
        var parent = TimeSpan.FromMinutes(5);
        var child = TimeSpan.FromHours(1);

        Assert.Equal(child, SettingMerge.Max(parent, child));
    }

    #endregion

    #region Set meet/join

    [Fact]
    public void Intersect_ReturnsCommonElements()
    {
        string[] parent = ["a", "b", "c"];
        string[] child = ["b", "c", "d"];

        Assert.Equal(["b", "c"], SettingMerge.Intersect(parent, child));
    }

    [Fact]
    public void Intersect_WhenDisjoint_ReturnsEmpty()
    {
        string[] parent = ["a"];
        string[] child = ["b"];

        Assert.Empty(SettingMerge.Intersect(parent, child));
    }

    [Fact]
    public void Union_ReturnsDeduplicatedElements()
    {
        string[] parent = ["a", "b"];
        string[] child = ["b", "c"];

        Assert.Equal(["a", "b", "c"], SettingMerge.Union(parent, child));
    }

    #endregion
}

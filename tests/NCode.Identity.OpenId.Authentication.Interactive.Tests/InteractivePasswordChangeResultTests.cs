// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractivePasswordChangeResultTests
{
    #region Changed Tests

    [Fact]
    public void Changed_IsChangedWithoutAnError()
    {
        var result = InteractivePasswordChangeResult.Changed;

        Assert.True(result.IsChanged);
        Assert.Null(result.Error);
    }

    #endregion

    #region NotSupported Tests

    [Fact]
    public void NotSupported_CarriesTheNotSupportedCode()
    {
        var result = InteractivePasswordChangeResult.NotSupported;

        Assert.False(result.IsChanged);
        Assert.Equal(InteractivePasswordChangeErrors.NotSupported, result.Error);
    }

    #endregion

    #region CurrentPasswordInvalid Tests

    [Fact]
    public void CurrentPasswordInvalid_CarriesTheInvalidCurrentPasswordCode()
    {
        var result = InteractivePasswordChangeResult.CurrentPasswordInvalid;

        Assert.False(result.IsChanged);
        Assert.Equal(InteractivePasswordChangeErrors.InvalidCurrentPassword, result.Error);
    }

    #endregion

    #region Refused Tests

    [Fact]
    public void Refused_CarriesTheGivenCode()
    {
        var result = InteractivePasswordChangeResult.Refused("password_too_short");

        Assert.False(result.IsChanged);
        Assert.Equal("password_too_short", result.Error);
    }

    [Fact]
    public void Refused_WhenTheCodeIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => InteractivePasswordChangeResult.Refused(""));
    }

    #endregion
}

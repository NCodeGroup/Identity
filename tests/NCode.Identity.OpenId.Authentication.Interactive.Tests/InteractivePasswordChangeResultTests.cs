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

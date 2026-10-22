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

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class BaseApiEndpointHandlerTests
{
    #region LimitErrorsPerSetting Tests

    [Fact]
    public void LimitErrorsPerSetting_WhenNoSettingExceedsTheLimit_KeepsEveryError()
    {
        SettingError[] errors = [new("a", "a1"), new("b", "b1"), new("a", "a2")];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 2);

        Assert.Equal(errors, result);
    }

    [Fact]
    public void LimitErrorsPerSetting_WhenASettingExceedsTheLimit_KeepsTheFirstAndCountsTheRest()
    {
        SettingError[] errors =
        [
            new("a", "a1"),
            new("a", "a2"),
            new("a", "a3"),
            new("a", "a4"),
            new("a", "a5"),
        ];

        SettingError[] expected =
        [
            new("a", "a1"),
            new("a", "a2"),
            new("a", "Errors not listed for 'a': 3."),
        ];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 2);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void LimitErrorsPerSetting_LimitsEachSettingSeparately()
    {
        SettingError[] errors =
        [
            new("a", "a1"),
            new("b", "b1"),
            new("a", "a2"),
            new("b", "b2"),
            new("a", "a3"),
            new("c", "c1"),
            new("b", "b3"),
            new("b", "b4"),
        ];

        SettingError[] expected =
        [
            new("a", "a1"),
            new("b", "b1"),
            new("c", "c1"),
            new("a", "Errors not listed for 'a': 2."),
            new("b", "Errors not listed for 'b': 3."),
        ];

        var result = BaseApiEndpointHandler.LimitErrorsPerSetting(errors, 1);

        Assert.Equal(expected, result);
    }

    #endregion
}

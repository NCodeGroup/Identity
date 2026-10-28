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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class SettingAlgorithmsTests : BaseTests
{
    #region IsSelectable Tests

    [Theory]
    [InlineData("", false)]
    [InlineData("none", false)]
    [InlineData("dir", false)]
    [InlineData("RSA1_5", false)]
    [InlineData("RSA-OAEP-256", true)]
    [InlineData("PS256", true)]
    public void IsSelectable_WhenGivenCode_ReturnsExpected(string code, bool expected)
    {
        var mockAlgorithm = CreateStrictMock<Algorithm>();
        mockAlgorithm.Setup(x => x.Code).Returns(code).Verifiable();

        var result = SettingAlgorithms.IsSelectable(mockAlgorithm.Object);

        Assert.Equal(expected, result);
    }

    #endregion
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

public class DefaultOpenIdMessageFactorySelectorTests : BaseTests
{
    private Mock<IOpenIdMessageFactory> CreateFactory(string typeDiscriminator)
    {
        var mock = CreateStrictMock<IOpenIdMessageFactory>();
        mock.Setup(x => x.TypeDiscriminator).Returns(typeDiscriminator).Verifiable();
        return mock;
    }

    #region GetFactory Tests

    [Fact]
    public void GetFactory_WhenPresent_ReturnsMatchingFactory()
    {
        var factory = CreateFactory("known");
        var selector = new DefaultOpenIdMessageFactorySelector([factory.Object]);

        var result = selector.GetFactory("known");

        Assert.Same(factory.Object, result);
    }

    [Fact]
    public void GetFactory_WhenMissing_ThrowsInvalidOperation()
    {
        var factory = CreateFactory("known");
        var selector = new DefaultOpenIdMessageFactorySelector([factory.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            selector.GetFactory("missing")
        );

        Assert.Contains("missing", exception.Message);
    }

    #endregion
}

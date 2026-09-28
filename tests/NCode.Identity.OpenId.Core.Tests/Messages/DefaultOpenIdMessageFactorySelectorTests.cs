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

using Moq;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages;

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

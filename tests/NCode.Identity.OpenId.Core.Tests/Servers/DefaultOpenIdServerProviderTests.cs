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

using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class DefaultOpenIdServerProviderTests : BaseTests
{
    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenItCreatesTheServer_RegistersItOnce()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockServer = CreateStrictMock<OpenIdServer>();

        var mockFactory = CreateStrictMock<IOpenIdServerFactory>();
        mockFactory
            .Setup(x => x.CreateAsync(mockEnvironment.Object, CancellationToken.None))
            .Returns(ValueTask.FromResult(mockServer.Object))
            .Verifiable(Times.Once());

        var mockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        mockRegistry.Setup(x => x.Register(mockServer.Object)).Verifiable(Times.Once());

        await using var provider = new DefaultOpenIdServerProvider(
            mockFactory.Object,
            mockRegistry.Object
        );

        var first = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);
        var second = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        Assert.Same(mockServer.Object, first);
        Assert.Same(mockServer.Object, second);
    }

    #endregion
}

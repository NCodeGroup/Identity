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

using Microsoft.Extensions.Http;
using Moq;
using NCode.Identity.OpenId.Client.MutualTls;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.MutualTls;

public class MutualTlsHttpClientOptionsSetupTests : BaseTests
{
    private Mock<ClientCertificateProvider> MockProvider { get; }
    private MutualTlsHttpClientOptionsSetup Setup { get; }

    public MutualTlsHttpClientOptionsSetupTests()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        MockProvider = CreateStrictMock<ClientCertificateProvider>(
            mockKeyRingProvider.Object,
            TimeProvider.System
        );
        Setup = new MutualTlsHttpClientOptionsSetup(MockProvider.Object);
    }

    private sealed class TestHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
    {
        public override string? Name { get; set; }

        public override HttpMessageHandler PrimaryHandler { get; set; } = null!;

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override HttpMessageHandler Build() => PrimaryHandler;
    }

    #region Configure Tests

    [Fact]
    public void Configure_WithUnrelatedName_AddsNoAction()
    {
        var options = new HttpClientFactoryOptions();

        Setup.Configure("other", options);

        Assert.Empty(options.HttpMessageHandlerBuilderActions);
    }

    [Fact]
    public void Configure_WithDefaultOverload_AddsNoAction()
    {
        var options = new HttpClientFactoryOptions();

        Setup.Configure(options);

        Assert.Empty(options.HttpMessageHandlerBuilderActions);
    }

    [Fact]
    public void Configure_WithPrefixedName_SetsThePrimaryHandler()
    {
        using var handler = new SocketsHttpHandler();
        MockProvider.Setup(x => x.CreatePrimaryHandler("ABC")).Returns(handler).Verifiable();
        var options = new HttpClientFactoryOptions();

        Setup.Configure(OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + "ABC", options);

        var action = Assert.Single(options.HttpMessageHandlerBuilderActions);
        var builder = new TestHttpMessageHandlerBuilder();
        action(builder);
        Assert.Same(handler, builder.PrimaryHandler);
    }

    #endregion
}

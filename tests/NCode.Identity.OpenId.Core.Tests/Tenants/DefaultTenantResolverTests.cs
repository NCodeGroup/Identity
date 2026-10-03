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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultTenantResolverTests
{
    [Fact]
    public void TryGetTenantId_DelegatesToSelectedStrategy()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = mocks.Create<ITenantStrategy>();

        strategy
            .Setup(x => x.StrategyCode)
            .Returns(OpenIdConstants.TenantStrategyCodes.DynamicByPath)
            .Verifiable();

        var expectedTenantId = "tenant-1";
        strategy
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out expectedTenantId))
            .Returns(true)
            .Verifiable();

        var options = Options.Create(
            new TenantResolutionOptions
            {
                StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            }
        );

        var resolver = new DefaultTenantResolver(options, [strategy.Object]);

        var result = resolver.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-1", tenantId);
        mocks.Verify();
    }
}

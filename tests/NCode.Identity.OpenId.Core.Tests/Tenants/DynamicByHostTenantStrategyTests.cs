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

public sealed class DynamicByHostTenantStrategyTests
{
    [Fact]
    public void TryGetTenantId_AlwaysReturnsFalse_BecauseDomainMappingRequiresStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = new DynamicByHostTenantStrategy(
            storeManagerFactory.Object,
            Options.Create(new TenantResolutionOptions())
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("tenant-1.example.com");

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.False(result);
        Assert.Null(tenantId);
        mocks.Verify();
    }
}

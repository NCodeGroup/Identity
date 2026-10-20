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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientAssertionReplayCacheTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private static DateTimeOffset ExpiresWhen { get; } = new(2026, 10, 19, 12, 5, 0, TimeSpan.Zero);

    private Mock<IExpiringKeySet> MockKeySet { get; }

    private OpenIdContext Context { get; }

    public DefaultClientAssertionReplayCacheTests()
    {
        MockKeySet = CreateStrictMock<IExpiringKeySet>();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        Context = mockContext.Object;
    }

    #region TryRecordAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryRecordAsync_AddsTheTenantScopedKeyUntilExpiry(bool added)
    {
        var expectedKey = ExpiringKeys.Create(
            "client_assertion_jti",
            TenantId,
            "client-1",
            "jti-1"
        );
        MockKeySet
            .Setup(x => x.TryAddAsync(expectedKey, ExpiresWhen, CancellationToken.None))
            .ReturnsAsync(added)
            .Verifiable();
        var cache = new DefaultClientAssertionReplayCache(MockKeySet.Object);

        var recorded = await cache.TryRecordAsync(
            Context,
            "client-1",
            "jti-1",
            ExpiresWhen,
            CancellationToken.None
        );

        Assert.Equal(added, recorded);
    }

    #endregion
}

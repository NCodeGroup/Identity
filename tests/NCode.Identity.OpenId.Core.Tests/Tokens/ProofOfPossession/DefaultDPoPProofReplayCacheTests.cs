// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public sealed class DefaultDPoPProofReplayCacheTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private static DateTimeOffset ExpiresWhen { get; } = new(2026, 10, 19, 12, 5, 0, TimeSpan.Zero);

    private Mock<IExpiringKeySet> MockKeySet { get; }

    private OpenIdContext Context { get; }

    public DefaultDPoPProofReplayCacheTests()
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
        var expectedKey = ExpiringKeys.Create("dpop_jti", TenantId, "jkt", "jti-1");
        MockKeySet
            .Setup(x => x.TryAddAsync(expectedKey, ExpiresWhen, CancellationToken.None))
            .ReturnsAsync(added)
            .Verifiable();
        var cache = new DefaultDPoPProofReplayCache(MockKeySet.Object);

        var recorded = await cache.TryRecordAsync(
            Context,
            "jkt",
            "jti-1",
            ExpiresWhen,
            CancellationToken.None
        );

        Assert.Equal(added, recorded);
    }

    #endregion
}

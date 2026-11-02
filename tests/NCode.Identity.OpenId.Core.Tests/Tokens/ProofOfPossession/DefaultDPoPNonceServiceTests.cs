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

public sealed class DefaultDPoPNonceServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private static DateTimeOffset Now { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private Mock<IExpiringKeySet> MockKeySet { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private Mock<OpenIdContext> MockContext { get; }

    private DefaultDPoPNonceService Service { get; }

    public DefaultDPoPNonceServiceTests()
    {
        MockKeySet = CreateStrictMock<IExpiringKeySet>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockContext = CreateStrictMock<OpenIdContext>();
        Service = new DefaultDPoPNonceService(MockKeySet.Object, MockTimeProvider.Object);
    }

    private void SetupTenant()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    #region IssueNonceAsync Tests

    [Fact]
    public async Task IssueNonceAsync_RecordsTheTenantScopedNonceForFiveMinutes()
    {
        SetupTenant();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        string? recordedKey = null;
        MockKeySet
            .Setup(x =>
                x.TryAddAsync(It.IsAny<string>(), Now.AddMinutes(5), CancellationToken.None)
            )
            .Callback<string, DateTimeOffset, CancellationToken>((key, _, _) => recordedKey = key)
            .ReturnsAsync(true)
            .Verifiable();

        var nonce = await Service.IssueNonceAsync(MockContext.Object, CancellationToken.None);

        Assert.Equal(32, nonce.Length);
        Assert.Equal(ExpiringKeys.Create("dpop_nonce", TenantId, nonce), recordedKey);
    }

    [Fact]
    public async Task IssueNonceAsync_WhenCalledTwice_IssuesDistinctNonces()
    {
        SetupTenant();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockKeySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var first = await Service.IssueNonceAsync(MockContext.Object, CancellationToken.None);
        var second = await Service.IssueNonceAsync(MockContext.Object, CancellationToken.None);

        Assert.NotEqual(first, second);
    }

    #endregion

    #region ValidateNonceAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateNonceAsync_ReturnsWhetherTheTenantScopedNonceIsRecorded(bool recorded)
    {
        SetupTenant();
        var expectedKey = ExpiringKeys.Create("dpop_nonce", TenantId, "nonce-1");
        MockKeySet
            .Setup(x => x.ContainsAsync(expectedKey, CancellationToken.None))
            .ReturnsAsync(recorded)
            .Verifiable();

        var valid = await Service.ValidateNonceAsync(
            MockContext.Object,
            "nonce-1",
            CancellationToken.None
        );

        Assert.Equal(recorded, valid);
    }

    [Fact]
    public async Task ValidateNonceAsync_WhenEmpty_ReturnsFalseWithoutALookup()
    {
        var valid = await Service.ValidateNonceAsync(
            MockContext.Object,
            string.Empty,
            CancellationToken.None
        );

        Assert.False(valid);
    }

    #endregion
}

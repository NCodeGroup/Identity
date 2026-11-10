// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public class DefaultGrantPayloadProtectorTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string GrantType = "authorization_code";
    private const string HashedKey = "hashed-1";
    private const string Secret = "secret-value";

    private static JsonSerializerOptions JsonOptions { get; } = new();

    private Mock<ISecretProtectorProvider> MockProvider { get; }
    private FakeSecretProtector Fake { get; }
    private DefaultGrantPayloadProtector Protector { get; }

    public DefaultGrantPayloadProtectorTests()
    {
        MockProvider = CreateStrictMock<ISecretProtectorProvider>();
        Fake = new FakeSecretProtector(SecretOwner.ForTenant(TenantId));
        Protector = new DefaultGrantPayloadProtector(
            MockProvider.Object,
            new DefaultPersistedPayloadSerializer([]),
            NullLogger<DefaultGrantPayloadProtector>.Instance
        );
    }

    private static Dictionary<string, string> CreatePayload() => new() { ["token"] = Secret };

    private void SetupProvider() =>
        MockProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<ISecretProtector>(Fake))
            .Verifiable();

    private ValueTask<string> ProtectAsync(CancellationToken cancellationToken) =>
        Protector.ProtectAsync(
            TenantId,
            HashedKey,
            CreatePayload(),
            JsonOptions,
            cancellationToken
        );

    private ValueTask<GrantPayload<Dictionary<string, string>>?> UnprotectAsync(
        string hashedKey,
        string protectedPayload,
        CancellationToken cancellationToken
    ) =>
        Protector.UnprotectOrDefaultAsync<Dictionary<string, string>>(
            TenantId,
            GrantType,
            hashedKey,
            protectedPayload,
            JsonOptions,
            cancellationToken
        );

    #region ProtectAsync Tests

    [Fact]
    public async Task ProtectAsync_StoresNoPlaintextAndBindsTheHashedKey()
    {
        SetupProvider();

        var result = await ProtectAsync(CancellationToken.None);

        Assert.DoesNotContain(Secret, result);
        Assert.StartsWith("fake.", result);
        Assert.Equal(SecretProtectionContext.ForGrantPayload(HashedKey), Fake.Contexts[result]);
    }

    #endregion

    #region UnprotectOrDefaultAsync Tests

    [Fact]
    public async Task UnprotectOrDefaultAsync_RoundTripsThePayload()
    {
        SetupProvider();
        var protectedPayload = await ProtectAsync(CancellationToken.None);

        var result = await UnprotectAsync(HashedKey, protectedPayload, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Secret, result.Value["token"]);
    }

    [Fact]
    public async Task UnprotectOrDefaultAsync_WhenTheHashedKeyDiffers_ReturnsNull()
    {
        SetupProvider();
        var protectedPayload = await ProtectAsync(CancellationToken.None);

        var result = await UnprotectAsync("hashed-2", protectedPayload, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UnprotectOrDefaultAsync_WhenTheMasterKeyCannotBeLoaded_ReturnsNull()
    {
        MockProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CryptographicException("no key"))
            .Verifiable();

        var result = await UnprotectAsync(HashedKey, "fake.AAAA", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion
}

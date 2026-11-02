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

    private static readonly JsonSerializerOptions JsonOptions = new();

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

    private ValueTask<string> ProtectAsync() =>
        Protector.ProtectAsync(
            TenantId,
            HashedKey,
            CreatePayload(),
            JsonOptions,
            CancellationToken.None
        );

    private ValueTask<GrantPayload<Dictionary<string, string>>?> UnprotectAsync(
        string hashedKey,
        string protectedPayload
    ) =>
        Protector.UnprotectOrDefaultAsync<Dictionary<string, string>>(
            TenantId,
            GrantType,
            hashedKey,
            protectedPayload,
            JsonOptions,
            CancellationToken.None
        );

    #region ProtectAsync Tests

    [Fact]
    public async Task ProtectAsync_StoresNoPlaintextAndBindsTheHashedKey()
    {
        SetupProvider();

        var result = await ProtectAsync();

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
        var protectedPayload = await ProtectAsync();

        var result = await UnprotectAsync(HashedKey, protectedPayload);

        Assert.NotNull(result);
        Assert.Equal(Secret, result.Value["token"]);
    }

    [Fact]
    public async Task UnprotectOrDefaultAsync_WhenTheHashedKeyDiffers_ReturnsNull()
    {
        SetupProvider();
        var protectedPayload = await ProtectAsync();

        var result = await UnprotectAsync("hashed-2", protectedPayload);

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
            .Returns(
                ValueTask.FromException<ISecretProtector>(new CryptographicException("no key"))
            )
            .Verifiable();

        var result = await UnprotectAsync(HashedKey, "fake.AAAA");

        Assert.Null(result);
    }

    #endregion
}

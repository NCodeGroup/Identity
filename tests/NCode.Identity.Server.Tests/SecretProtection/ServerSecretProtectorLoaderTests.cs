// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ServerSecretProtectorLoaderTests : BaseTests
{
    private const string ServerId = "server-a";
    private static readonly DateTimeOffset Now = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SecretOwner Owner = SecretOwner.ForServer(ServerId);

    private static readonly Guid ActiveKey = Guid.NewGuid();
    private static readonly Guid RetiredLiveKey = Guid.NewGuid();
    private static readonly Guid RetiredExpiredKey = Guid.NewGuid();
    private static readonly Guid MissingKey = Guid.NewGuid();

    private static XElement RingKey(Guid masterKeyId, DateTimeOffset expires) =>
        new(
            "key",
            new XElement("expirationDate", expires.ToString("O")),
            new XElement(
                MasterKeyXmlFormat.EncryptedSecretName,
                new XElement(MasterKeyXmlFormat.ValueName, FakeRingProvider.Value(masterKeyId))
            )
        );

    private ServerSecretProtectorLoader CreateLoader(
        FakeRingProvider provider,
        ServerSecretProtectorHolder holder,
        params XElement[] ringKeys
    )
    {
        var repository = CreateStrictMock<IXmlRepository>();
        repository.Setup(x => x.GetAllElements()).Returns(ringKeys).Verifiable();

        return CreateLoader(
            provider,
            holder,
            new DataProtectionKeyRingReader(
                Options.Create(new KeyManagementOptions { XmlRepository = repository.Object })
            )
        );
    }

    private static ServerSecretProtectorLoader CreateLoader(
        FakeRingProvider provider,
        ServerSecretProtectorHolder holder,
        DataProtectionKeyRingReader reader
    ) =>
        new(
            provider,
            reader,
            Options.Create(
                new OpenIdOptions { Server = new OpenIdServerOptions { ServerId = ServerId } }
            ),
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(Now),
            holder,
            NullLogger<ServerSecretProtectorLoader>.Instance
        );

    private static ServerSecretProtectorLoader CreateLoaderWithoutRing() =>
        CreateLoader(
            new FakeRingProvider(),
            new ServerSecretProtectorHolder(),
            new DataProtectionKeyRingReader(Options.Create(new KeyManagementOptions()))
        );

    #region LoadAsync Tests

    [Fact]
    public async Task LoadAsync_ALiveRingKeyNamesARetiredKey_LoadsIt()
    {
        var provider = new FakeRingProvider(ActiveKey, RetiredLiveKey);
        var holder = new ServerSecretProtectorHolder();
        var loader = CreateLoader(
            provider,
            holder,
            RingKey(ActiveKey, Now.AddDays(60)),
            RingKey(RetiredLiveKey, Now.AddDays(10)),
            RingKey(RetiredLiveKey, Now.AddDays(20))
        );

        await loader.LoadAsync(CancellationToken.None);

        Assert.NotNull(holder.Protector);
        // One probe per master key, then the protector that holds them all.
        var last = provider.Requests[^1];
        Assert.Equal(Owner, last.Owner);
        Assert.Equal(2, last.Values.Count);
        Assert.Equal(3, provider.Requests.Count);
    }

    [Fact]
    public async Task LoadAsync_ARingKeyExpiredPastTheGrace_IsIgnored()
    {
        var provider = new FakeRingProvider(ActiveKey);
        var holder = new ServerSecretProtectorHolder();
        var loader = CreateLoader(provider, holder, RingKey(RetiredExpiredKey, Now.AddDays(-31)));

        await loader.LoadAsync(CancellationToken.None);

        var request = Assert.Single(provider.Requests);
        Assert.Empty(request.Values);
        Assert.NotNull(holder.Protector);
    }

    [Fact]
    public async Task LoadAsync_AMasterKeyThatDoesNotLoad_IsSkippedSoTheGateStillSeesItsRingKey()
    {
        var provider = new FakeRingProvider(ActiveKey);
        var holder = new ServerSecretProtectorHolder();
        var loader = CreateLoader(
            provider,
            holder,
            RingKey(ActiveKey, Now.AddDays(60)),
            RingKey(MissingKey, Now.AddDays(10))
        );

        await loader.LoadAsync(CancellationToken.None);

        Assert.NotNull(holder.Protector);
        Assert.Single(provider.Requests[^1].Values);
    }

    #endregion

    #region Owner Tests

    [Fact]
    public void Owner_WithAConfiguredServerId_IsThatServer()
    {
        Assert.Equal(Owner, CreateLoaderWithoutRing().Owner);
    }

    #endregion

    #region IsOwner Tests

    [Fact]
    public void IsOwner_TheConfiguredServer_ReturnsTrue()
    {
        Assert.True(CreateLoaderWithoutRing().IsOwner(Owner));
    }

    [Fact]
    public void IsOwner_TheServerInAnotherCase_ReturnsTrue()
    {
        Assert.True(CreateLoaderWithoutRing().IsOwner(SecretOwner.ForServer("SERVER-A")));
    }

    [Fact]
    public void IsOwner_ATenantWithTheServersId_ReturnsFalse()
    {
        Assert.False(CreateLoaderWithoutRing().IsOwner(SecretOwner.ForTenant(ServerId)));
    }

    [Fact]
    public void IsOwner_AnotherServer_ReturnsFalse()
    {
        Assert.False(CreateLoaderWithoutRing().IsOwner(SecretOwner.ForServer("server-b")));
    }

    #endregion
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
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
        repository.Setup(x => x.GetAllElements()).Returns(ringKeys);

        return new ServerSecretProtectorLoader(
            provider,
            new DataProtectionKeyRingReader(
                Options.Create(new KeyManagementOptions { XmlRepository = repository.Object })
            ),
            Options.Create(
                new OpenIdOptions { Server = new OpenIdServerOptions { ServerId = ServerId } }
            ),
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(Now),
            holder,
            CreateLooseMock<Microsoft.Extensions.Logging.ILogger<ServerSecretProtectorLoader>>().Object
        );
    }

    [Fact]
    public async Task LoadAsync_LoadsEveryRetiredKeyThatALiveRingKeyNames()
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
    public async Task LoadAsync_IgnoresARingKeyThatExpiredPastTheGrace()
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

    [Fact]
    public void Owner_UsesTheConfiguredServerId()
    {
        var loader = CreateLoader(new FakeRingProvider(), new ServerSecretProtectorHolder());

        Assert.Equal(Owner, loader.Owner);
    }
}

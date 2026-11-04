// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DataProtectionKeyRingValueSourceTests : BaseTests
{
    private const string ServerId = "server-a";
    private static readonly DateTimeOffset Now = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SecretOwner Owner = SecretOwner.ForServer(ServerId);
    private static readonly Guid KeyId = Guid.NewGuid();

    private DataProtectionKeyRingValueSource CreateSource(
        DataProtectionKeyRingReader reader,
        FakeRingProvider provider
    )
    {
        var loader = new ServerSecretProtectorLoader(
            provider,
            reader,
            Options.Create(
                new OpenIdOptions { Server = new OpenIdServerOptions { ServerId = ServerId } }
            ),
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(Now),
            new ServerSecretProtectorHolder(),
            CreateLooseMock<ILogger<ServerSecretProtectorLoader>>().Object
        );

        return new DataProtectionKeyRingValueSource(
            loader,
            reader,
            provider,
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(Now)
        );
    }

    private DataProtectionKeyRingReader CreateReader(params KeyRingValue[] values)
    {
        var mock = CreatePartialMock<DataProtectionKeyRingReader>(
            Options.Create(
                new Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions()
            )
        );
        IReadOnlyList<KeyRingValue> read = values;
        mock.Setup(x => x.TryRead(out read)).Returns(true);
        return mock.Object;
    }

    [Fact]
    public async Task Rewrap_RewrapsNothing()
    {
        var source = CreateSource(CreateReader(), new FakeRingProvider());

        Assert.Equal(0, await source.RewrapAsync(Owner, KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task References_ALiveRingKeyNamesTheKey_ReturnsTrue()
    {
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(5))),
            new FakeRingProvider()
        );

        Assert.True(await source.ReferencesAsync(Owner, KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task References_ARingKeyExpiredWithinTheGrace_ReturnsTrue()
    {
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(-10))),
            new FakeRingProvider()
        );

        Assert.True(await source.ReferencesAsync(Owner, KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task References_OnlyARingKeyExpiredPastTheGraceNamesTheKey_ReturnsFalse()
    {
        var source = CreateSource(
            CreateReader(
                new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(-31)),
                new KeyRingValue(FakeRingProvider.Value(Guid.NewGuid()), Now.AddDays(5))
            ),
            new FakeRingProvider()
        );

        Assert.False(await source.ReferencesAsync(Owner, KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task References_AnUnreadableRing_ReturnsTrue()
    {
        var reader = new DataProtectionKeyRingReader(
            Options.Create(
                new Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions()
            )
        );
        var source = CreateSource(reader, new FakeRingProvider());

        Assert.True(await source.ReferencesAsync(Owner, KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task References_AnotherOwner_ReturnsFalse()
    {
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(5))),
            new FakeRingProvider()
        );

        Assert.False(
            await source.ReferencesAsync(
                SecretOwner.ForTenant(ServerId),
                KeyId,
                CancellationToken.None
            )
        );
    }
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DataProtectionKeyRingValueSourceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SecretOwner Owner = SecretOwner.Platform;
    private static readonly Guid KeyId = Guid.NewGuid();

    private DataProtectionKeyRingValueSource CreateSource(
        DataProtectionKeyRingReader reader,
        FakeRingProvider provider
    )
    {
        var loader = new PlatformSecretProtectorLoader(
            provider,
            reader,
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(Now),
            new PlatformSecretProtectorHolder(),
            CreateLooseMock<ILogger<PlatformSecretProtectorLoader>>().Object
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
            Options.Create(new KeyManagementOptions())
        );
        IReadOnlyList<KeyRingValue> read = values;
        mock.Setup(x => x.TryRead(out read)).Returns(true).Verifiable();
        return mock.Object;
    }

    // A ring that cannot be read; no test that uses it reads the ring.
    private static DataProtectionKeyRingReader CreateUnreadableReader() =>
        new(Options.Create(new KeyManagementOptions()));

    private static readonly IReadOnlySet<Guid> Keys = new HashSet<Guid> { KeyId };

    #region RewrapAsync Tests

    [Fact]
    public async Task RewrapAsync_AnyKeys_ReturnsNone()
    {
        var source = CreateSource(CreateUnreadableReader(), new FakeRingProvider());

        Assert.Equal(
            ProtectedValueRewrapResult.None,
            await source.RewrapAsync(Owner, Keys, CancellationToken.None)
        );
    }

    #endregion

    #region FindReferencedAsync Tests

    [Fact]
    public async Task FindReferenced_ALiveRingKeyNamesTheKey_ReportsIt()
    {
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(5))),
            new FakeRingProvider()
        );

        Assert.Equal(
            [KeyId],
            await source.FindReferencedAsync(Owner, Keys, CancellationToken.None)
        );
    }

    [Fact]
    public async Task FindReferenced_SeveralKeys_ReturnsOnlyThoseALiveRingKeyNames()
    {
        var other = Guid.NewGuid();
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(5))),
            new FakeRingProvider()
        );

        var found = await source.FindReferencedAsync(
            Owner,
            new HashSet<Guid> { KeyId, other },
            CancellationToken.None
        );

        Assert.Equal([KeyId], found);
    }

    [Fact]
    public async Task FindReferenced_ARingKeyExpiredWithinTheGrace_ReportsIt()
    {
        var source = CreateSource(
            CreateReader(new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(-10))),
            new FakeRingProvider()
        );

        Assert.Equal(
            [KeyId],
            await source.FindReferencedAsync(Owner, Keys, CancellationToken.None)
        );
    }

    [Fact]
    public async Task FindReferenced_OnlyARingKeyExpiredPastTheGraceNamesTheKey_ReportsNone()
    {
        var source = CreateSource(
            CreateReader(
                new KeyRingValue(FakeRingProvider.Value(KeyId), Now.AddDays(-31)),
                new KeyRingValue(FakeRingProvider.Value(Guid.NewGuid()), Now.AddDays(5))
            ),
            new FakeRingProvider()
        );

        Assert.Empty(await source.FindReferencedAsync(Owner, Keys, CancellationToken.None));
    }

    [Fact]
    public async Task FindReferenced_AnUnreadableRing_ReturnsEveryKey()
    {
        var source = CreateSource(CreateUnreadableReader(), new FakeRingProvider());

        Assert.Equal(
            [KeyId],
            await source.FindReferencedAsync(Owner, Keys, CancellationToken.None)
        );
    }

    [Fact]
    public async Task FindReferenced_AnotherOwner_ReturnsNone()
    {
        var source = CreateSource(CreateUnreadableReader(), new FakeRingProvider());

        Assert.Empty(
            await source.FindReferencedAsync(
                SecretOwner.ForTenant(SecretOwner.PlatformOwnerId),
                Keys,
                CancellationToken.None
            )
        );
    }

    #endregion
}

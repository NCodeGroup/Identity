// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class MasterKeyRingEntryTests
{
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("tenant-one");

    private static MasterKey CreateMasterKey(Guid keyId) =>
        MasterKey.Create(
            keyId,
            Owner,
            1,
            RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes)
        );

    #region Contains Tests

    [Fact]
    public void Contains_EmptySet_ReturnsTrue()
    {
        var keyId = Guid.NewGuid();
        using var masterKey = CreateMasterKey(keyId);
        var entry = new MasterKeyRingEntry(
            masterKey,
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary(),
            0
        );

        var result = entry.Contains(FrozenSet<Guid>.Empty);

        Assert.True(result);
    }

    [Fact]
    public void Contains_AllCached_ReturnsTrue()
    {
        var activeKeyId = Guid.NewGuid();
        var olderKeyId = Guid.NewGuid();
        using var activeKey = CreateMasterKey(activeKeyId);
        using var olderKey = CreateMasterKey(olderKeyId);
        var entry = new MasterKeyRingEntry(
            activeKey,
            new Dictionary<Guid, MasterKey>
            {
                [activeKeyId] = activeKey,
                [olderKeyId] = olderKey,
            }.ToFrozenDictionary(),
            0
        );

        var result = entry.Contains(new HashSet<Guid> { activeKeyId, olderKeyId });

        Assert.True(result);
    }

    [Fact]
    public void Contains_OneMissing_ReturnsFalse()
    {
        var keyId = Guid.NewGuid();
        using var masterKey = CreateMasterKey(keyId);
        var entry = new MasterKeyRingEntry(
            masterKey,
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary(),
            0
        );

        var result = entry.Contains(new HashSet<Guid> { keyId, Guid.NewGuid() });

        Assert.False(result);
    }

    #endregion

    #region IsStale Tests

    [Fact]
    public void IsStale_BeforeTheInterval_ReturnsFalse()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var keyId = Guid.NewGuid();
        using var masterKey = CreateMasterKey(keyId);
        var entry = new MasterKeyRingEntry(
            masterKey,
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary(),
            time.GetTimestamp()
        );

        time.Advance(TimeSpan.FromMinutes(4));

        Assert.False(entry.IsStale(time, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void IsStale_AtTheInterval_ReturnsTrue()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var keyId = Guid.NewGuid();
        using var masterKey = CreateMasterKey(keyId);
        var entry = new MasterKeyRingEntry(
            masterKey,
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary(),
            time.GetTimestamp()
        );

        time.Advance(TimeSpan.FromMinutes(5));

        Assert.True(entry.IsStale(time, TimeSpan.FromMinutes(5)));
    }

    #endregion
}

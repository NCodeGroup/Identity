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
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary()
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
            }.ToFrozenDictionary()
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
            new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary()
        );

        var result = entry.Contains(new HashSet<Guid> { keyId, Guid.NewGuid() });

        Assert.False(result);
    }

    #endregion
}

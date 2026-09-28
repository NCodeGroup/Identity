#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

public class DefaultSecretKeyCollectionTests
{
    private static TestSecretKey CreateKey(string? keyId, DateTimeOffset? expiresWhen) =>
        new(new KeyMetadata { KeyId = keyId, ExpiresWhen = expiresWhen });

    #region Count Property Tests

    [Fact]
    public void Count_WhenItemsProvided_ReturnsCount()
    {
        var collection = new DefaultSecretKeyCollection([
            CreateKey("a", null),
            CreateKey("b", null),
        ]);

        Assert.Equal(2, collection.Count);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public void GetEnumerator_WhenEnumerated_OrdersByExpiresWhenDescending()
    {
        var earlier = CreateKey("earlier", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var later = CreateKey("later", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var never = CreateKey("never", null);

        var collection = new DefaultSecretKeyCollection([earlier, later, never]);

        var ordered = collection.ToList();

        Assert.Equal(["never", "later", "earlier"], ordered.Select(x => x.KeyId));
    }

    #endregion

    #region TryGetByKeyId Tests

    [Fact]
    public void TryGetByKeyId_WhenKeyExists_ReturnsTrue()
    {
        var key = CreateKey("target", null);
        var collection = new DefaultSecretKeyCollection([key, CreateKey("other", null)]);

        var found = collection.TryGetByKeyId("target", out var secretKey);

        Assert.True(found);
        Assert.Same(key, secretKey);
    }

    [Fact]
    public void TryGetByKeyId_WhenKeyMissing_ReturnsFalse()
    {
        var collection = new DefaultSecretKeyCollection([CreateKey("other", null)]);

        var found = collection.TryGetByKeyId("missing", out var secretKey);

        Assert.False(found);
        Assert.Null(secretKey);
    }

    [Fact]
    public void TryGetByKeyId_WhenKeysHaveNoId_AreExcludedFromLookup()
    {
        var collection = new DefaultSecretKeyCollection([CreateKey(null, null)]);

        var found = collection.TryGetByKeyId("", out var secretKey);

        Assert.False(found);
        Assert.Null(secretKey);
    }

    [Fact]
    public void TryGetByKeyId_WhenDuplicateKeyId_ReturnsFirstOrdered()
    {
        var first = CreateKey("dup", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var second = CreateKey("dup", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var collection = new DefaultSecretKeyCollection([second, first]);

        collection.TryGetByKeyId("dup", out var secretKey);

        Assert.Same(first, secretKey);
    }

    #endregion
}

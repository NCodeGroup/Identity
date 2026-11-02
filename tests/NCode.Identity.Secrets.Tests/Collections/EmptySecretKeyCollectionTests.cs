// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Collections;

public class EmptySecretKeyCollectionTests
{
    #region Singleton Tests

    [Fact]
    public void Singleton_WhenAccessed_ReturnsSameInstance()
    {
        Assert.Same(EmptySecretKeyCollection.Singleton, EmptySecretKeyCollection.Singleton);
    }

    #endregion

    #region Count Property Tests

    [Fact]
    public void Count_WhenEmpty_ReturnsZero()
    {
        Assert.Empty(EmptySecretKeyCollection.Singleton);
    }

    #endregion

    #region TryGetByKeyId Tests

    [Fact]
    public void TryGetByKeyId_WhenCalled_ReturnsFalse()
    {
        var found = EmptySecretKeyCollection.Singleton.TryGetByKeyId("anything", out var secretKey);

        Assert.False(found);
        Assert.Null(secretKey);
    }

    #endregion

    #region GetEnumerator Tests

    [Fact]
    public void GetEnumerator_WhenEnumerated_YieldsNothing()
    {
        var items = EmptySecretKeyCollection.Singleton.ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_YieldsNothing()
    {
        System.Collections.IEnumerable enumerable = EmptySecretKeyCollection.Singleton;

        var enumerator = enumerable.GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    #endregion
}

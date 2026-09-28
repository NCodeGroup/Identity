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

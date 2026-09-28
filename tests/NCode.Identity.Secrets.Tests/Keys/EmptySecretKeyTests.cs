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

namespace NCode.Identity.Secrets.Keys;

public class EmptySecretKeyTests
{
    #region Singleton Tests

    [Fact]
    public void Singleton_WhenAccessed_ReturnsSameInstance()
    {
        Assert.Same(EmptySecretKey.Singleton, EmptySecretKey.Singleton);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_WhenEmpty_ReturnDefaults()
    {
        var key = EmptySecretKey.Singleton;

        Assert.Equal(string.Empty, key.KeyType);
        Assert.Equal(default, key.Metadata);
        Assert.Equal(0, key.KeySizeBits);
        Assert.Equal(0, key.KeySizeBytes);
        Assert.Null(key.KeyId);
    }

    #endregion
}

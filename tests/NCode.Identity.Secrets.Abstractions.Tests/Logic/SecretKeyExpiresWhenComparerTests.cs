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

public class SecretKeyExpiresWhenComparerTests
{
    private static TestSecretKey CreateKey(DateTimeOffset? expiresWhen) =>
        new(new KeyMetadata { ExpiresWhen = expiresWhen });

    #region Compare Tests

    [Fact]
    public void Compare_WhenSameReference_ReturnsZero()
    {
        var key = CreateKey(DateTimeOffset.UnixEpoch);

        Assert.Equal(0, SecretKeyExpiresWhenComparer.Singleton.Compare(key, key));
    }

    [Fact]
    public void Compare_WhenBothNull_ReturnsZero()
    {
        Assert.Equal(0, SecretKeyExpiresWhenComparer.Singleton.Compare(null, null));
    }

    [Fact]
    public void Compare_WhenLaterExpiry_SortsBeforeEarlierExpiry()
    {
        var later = CreateKey(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var earlier = CreateKey(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(later, earlier);

        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_WhenNullExpiry_TreatedAsMaxValue()
    {
        var never = CreateKey(expiresWhen: null);
        var expires = CreateKey(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(never, expires);

        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_WhenSameExpiry_FallsBackToHashCode()
    {
        var expiresWhen = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var left = CreateKey(expiresWhen);
        var right = CreateKey(expiresWhen);

        var expected = left.GetHashCode().CompareTo(right.GetHashCode());
        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(left, right);

        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }

    #endregion
}

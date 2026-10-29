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

using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffSessionCodecTests : BaseTests
{
    private static BffSessionMetadata CreateMetadata(DateTimeOffset? idleExpiresAt = null) =>
        new()
        {
            Frontend = "default",
            Issuer = "https://op.example",
            Subject = "sub-1",
            SessionId = "sid-1",
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            LastAccessedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            AbsoluteExpiresAt = new DateTimeOffset(2026, 1, 2, 11, 4, 5, TimeSpan.Zero),
            IdleExpiresAt = idleExpiresAt ?? new DateTimeOffset(2026, 1, 2, 4, 4, 6, TimeSpan.Zero),
        };

    private static BffSessionPayload CreatePayload() =>
        new()
        {
            IdToken = "id-token",
            RefreshToken = "refresh",
            LogoutGuard = "guard",
        };

    #region TryGetStoreKey Tests

    [Fact]
    public void TryGetStoreKey_ValidHandle_ReturnsKey()
    {
        var handle = BffKeyDerivation.CreateSecret();

        var result = BffSessionCodec.TryGetStoreKey(handle, out var storeKey);

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(storeKey));
    }

    [Fact]
    public void TryGetStoreKey_InvalidHandle_ReturnsFalse()
    {
        var result = BffSessionCodec.TryGetStoreKey("short", out var storeKey);

        Assert.False(result);
        Assert.Null(storeKey);
    }

    #endregion

    #region Encode and TryDecode Tests

    [Fact]
    public void Encode_InvalidHandle_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BffSessionCodec.Encode("short", 1, CreateMetadata(), CreatePayload())
        );
    }

    [Fact]
    public void TryDecode_RoundTrip_ReturnsPayload()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(handle, 3, CreateMetadata(), CreatePayload());

        var result = BffSessionCodec.TryDecode(handle, record, out var payload);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Equal("id-token", payload.IdToken);
        Assert.Equal("refresh", payload.RefreshToken);
        Assert.Equal("guard", payload.LogoutGuard);
    }

    [Fact]
    public void TryDecode_AnotherHandle_ReturnsFalse()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var other = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(handle, 3, CreateMetadata(), CreatePayload());

        var result = BffSessionCodec.TryDecode(other, record, out var payload);

        Assert.False(result);
        Assert.Null(payload);
    }

    [Fact]
    public void TryDecode_VersionChanged_ReturnsFalse()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(handle, 3, CreateMetadata(), CreatePayload());
        var changed = new BffSessionRecord(
            record.StoreKey,
            4,
            record.Metadata,
            record.ProtectedPayload
        );

        var result = BffSessionCodec.TryDecode(handle, changed, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryDecode_IdleExpiryChanged_ReturnsFalse()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(handle, 3, CreateMetadata(), CreatePayload());
        var metadata = CreateMetadata(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));
        var changed = new BffSessionRecord(
            record.StoreKey,
            record.Version,
            metadata,
            record.ProtectedPayload
        );

        var result = BffSessionCodec.TryDecode(handle, changed, out _);

        Assert.False(result);
    }

    #endregion
}

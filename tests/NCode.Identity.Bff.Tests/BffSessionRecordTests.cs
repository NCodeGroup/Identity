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

using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffSessionRecordTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidArguments_EchoesProperties()
    {
        var metadata = new BffSessionMetadata();
        byte[] payload = [1, 2, 3];

        var record = new BffSessionRecord("key", 3, metadata, payload);

        Assert.Equal("key", record.StoreKey);
        Assert.Equal(3, record.Version);
        Assert.Same(metadata, record.Metadata);
        Assert.Equal(payload, record.ProtectedPayload.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_EmptyStoreKey_Throws(string? storeKey)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new BffSessionRecord(storeKey!, 1, new BffSessionMetadata(), ReadOnlyMemory<byte>.Empty)
        );
    }

    [Fact]
    public void Constructor_NegativeVersion_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BffSessionRecord("key", -1, new BffSessionMetadata(), ReadOnlyMemory<byte>.Empty)
        );
    }

    [Fact]
    public void Constructor_NullMetadata_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BffSessionRecord("key", 1, null!, ReadOnlyMemory<byte>.Empty)
        );
    }

    #endregion
}

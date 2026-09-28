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

public class KeyMetadataTests
{
    #region Property Tests

    [Fact]
    public void Properties_WhenSet_RoundTrip()
    {
        var expiresWhen = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var metadata = new KeyMetadata
        {
            KeyId = "kid",
            Use = "sig",
            Algorithm = "RS256",
            ExpiresWhen = expiresWhen,
        };

        Assert.Equal("kid", metadata.KeyId);
        Assert.Equal("sig", metadata.Use);
        Assert.Equal("RS256", metadata.Algorithm);
        Assert.Equal(expiresWhen, metadata.ExpiresWhen);
    }

    [Fact]
    public void Defaults_WhenNotSet_AreNull()
    {
        var metadata = new KeyMetadata();

        Assert.Null(metadata.KeyId);
        Assert.Null(metadata.Use);
        Assert.Null(metadata.Algorithm);
        Assert.Null(metadata.ExpiresWhen);
    }

    [Fact]
    public void Equality_WhenSameValues_AreEqual()
    {
        var left = new KeyMetadata { KeyId = "kid", Use = "sig" };
        var right = new KeyMetadata { KeyId = "kid", Use = "sig" };

        Assert.Equal(left, right);
    }

    #endregion
}
